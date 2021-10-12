using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using AniDbService.Extensions;
using AniDbService.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Caching.Distributed;

namespace AniDbService.Services;

public class AniDbService : AnimeDbService.AnimeDbServiceBase
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<AniDbService> _logger;

    public AniDbService(ILogger<AniDbService> logger, IConfiguration configuration, IDistributedCache distributedCache,
        IHttpClientFactory clientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _distributedCache = distributedCache;
        _clientFactory = clientFactory;
    }

    public override async Task GetUpComingAnime(Empty request, IServerStreamWriter<Anime> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("Attempting to get calender items");
        var items = await GetCalenderItems();
        if (items is not null)
            foreach (var item in items)
                await responseStream.WriteAsync(item);
    }

    private async Task<IEnumerable<Anime>?> GetCalenderItems()
    {
        var user = _configuration["AniDb_User"];
        var password = _configuration["AniDb_Password"];
        var aniDbClient = _configuration["AniDb_UDPClient"];
        var aniDbClientVersion = _configuration["AniDb_ClientVersion"];
        if (user is null || password is null || aniDbClient is null || aniDbClientVersion is null)
        {
            _logger.LogError("Error fetching anime info: AniDb credentials empty");
            return null;
        }

        var items = await _distributedCache.GetRecordAsync<Anime[]>("ANIME")
                    ?? Array.Empty<Anime>();
        if (items.Length != 0) return items;
        using UdpClient udpClient = new(11000);
        try
        {
            udpClient.Connect("api.anidb.net", 9000);

            // Sends a message to the host to which you have connected.
            var loginBytes =
                Encoding.ASCII.GetBytes(
                    $"AUTH user={user}&pass={password}&protover=3&client={aniDbClient}&clientver={aniDbClientVersion}");

            await udpClient.SendAsync(loginBytes, loginBytes.Length);

            //IPEndPoint object will allow us to read datagrams sent from any source.
            IPEndPoint remoteIpEndPoint = new(IPAddress.Any, 0);

            // Blocks until a message returns on this socket from a remote host.
            var loginReceiveBytes = udpClient.Receive(ref remoteIpEndPoint);
            var loginReturnData = Encoding.ASCII.GetString(loginReceiveBytes);
            if (loginReturnData.Contains("LOGIN ACCEPTED"))
            {
                var indexOfEnd = loginReturnData.IndexOf("LO", StringComparison.Ordinal) - 5;
                var sessionToken = loginReturnData.Substring(4, indexOfEnd);
                var calendarBytes = Encoding.ASCII.GetBytes($"CALENDAR s={sessionToken}");

                await udpClient.SendAsync(calendarBytes, calendarBytes.Length);
                var calendarReceiveBytes = udpClient.Receive(ref remoteIpEndPoint);
                var calendarReturnData = Encoding.ASCII.GetString(calendarReceiveBytes);
                if (calendarReturnData.Contains("297"))
                {
                    calendarReturnData = calendarReturnData.Remove(0, 13);
                    var lines = calendarReturnData.Split(
                        new[] {"\n"},
                        StringSplitOptions.None
                    );
                    var calendarItems = new HashSet<CalenderItem>();
                    foreach (var line in lines)
                    {
                        var item = ParseCalenderItem(line);
                        if (item is not null) calendarItems.Add(item);
                    }

                    if (calendarItems.Count > 0)
                    {
                        var anime = await GetAnimeInfo(calendarItems);
                        var enumerable = anime as Anime[] ?? anime.ToArray();
                        if (enumerable.Any())
                            await _distributedCache.SetRecordAsync("ANIME", enumerable, TimeSpan.FromDays(1)
                                .Add(TimeSpan.FromHours(12)));
                        return enumerable;
                    }
                }
            }
            else
            {
                _logger.LogCritical($"Login not accepted, returndata {loginReturnData}");
            }
        }
        catch (Exception e)
        {
            _logger.LogCritical($"Exception when getting calender items: {e.Message}");
        }

        return items;
    }

    private async Task<IEnumerable<Anime>> GetAnimeInfo(IEnumerable<CalenderItem> calendarItems)
    {
        var client = _clientFactory.CreateClient("anidb");
        var aniDbClient = _configuration["AniDb_HTTPClient"];
        var aniDbClientVersion = _configuration["AniDb_ClientVersion"];
        ConcurrentStack<Anime> animeResults = new();
        if (aniDbClient is null || aniDbClientVersion is null)
        {
            _logger.LogError("Error fetching anime info: AniDb credentials empty");
            return animeResults;
        }

        foreach (var calenderItem in calendarItems)
        {
            await Task.Delay(TimeSpan.FromSeconds(5)); //intentional
            var apiEndPoint =
                $"httpapi?client={aniDbClient}&clientver={aniDbClientVersion}&protover=1&request=anime&aid={calenderItem.Id}";
            var response = await client.GetAsync(apiEndPoint);
            if (response.IsSuccessStatusCode)
                try
                {
                    var anime = XElement.Load(await response.Content.ReadAsStreamAsync());
                    var title = anime.Descendants("titles").Select(x => x.Value).FirstOrDefault();
                    var img = anime.Descendants("picture").FirstOrDefault()?.Value;
                    if (title is not null)
                        animeResults.Push(new Anime
                        {
                            AnimeId = calenderItem.Id,
                            DateTime = DateTimeOffset.FromUnixTimeSeconds(calenderItem.UnixTimeInSeconds)
                                .ToTimestamp(),
                            DateType = calenderItem.DateFlag,
                            Name = title,
                            ImgUrl = img is null ? "UNDEFINED" : $"https://cdn-us.anidb.net/images/main/{img}"
                        });
                }
                catch (Exception e)
                {
                    _logger.LogError($"Error when getting anime info: {e.Message}");
                }
        }
        // await Parallel.ForEachAsync(calendarItems, async (calenderItem, token) =>
        // {
        //     var apiEndPoint =
        //         $"httpapi?client={aniDbClient}&clientver={aniDbClientVersion}&protover=1&request=anime&aid={calenderItem.Id}";
        //     var response = await client.GetAsync(apiEndPoint, token);
        //     if (response.IsSuccessStatusCode)
        //         try
        //         {
        //             var anime = XElement.Load(await response.Content.ReadAsStreamAsync(token));
        //             var title = anime.Descendants("titles").Select(x => x.Value).FirstOrDefault();
        //             var img = anime.Descendants("picture").FirstOrDefault()?.Value;
        //             if (title is not null)
        //                 animeResults.Push(new Anime
        //                 {
        //                     AnimeId = calenderItem.Id,
        //                     DateTime = DateTimeOffset.FromUnixTimeSeconds(calenderItem.UnixTimeInSeconds)
        //                         .ToTimestamp(),
        //                     DateType = calenderItem.DateFlag,
        //                     Name = title,
        //                     ImgUrl = img is null ? "UNDEFINED" : $"https://cdn-us.anidb.net/images/main/{img}"
        //                 });
        //         }
        //         catch (Exception e)
        //         {
        //             _logger.LogError($"Error when getting anime info: {e.Message}");
        //         }
        // });
        _logger.LogInformation($"Collected {animeResults.Count} anime information");
        return animeResults;
    }

    private static CalenderItem? ParseCalenderItem(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        //16553|1633046400|0
        var index1 = line.IndexOf('|');
        var animeStr = line[..index1];
        var end = index1 + 1;
        var index2 = line.IndexOf('|', end);
        var timeStr = line.Substring(end, index2 - end);
        if (!int.TryParse(timeStr, out var unixTime) || !int.TryParse(animeStr, out var animeId))
            return null;
        int.TryParse(line.AsSpan(index2 + 1), out var dateFlag);
        return new CalenderItem
        {
            Id = animeId,
            UnixTimeInSeconds = unixTime,
            DateFlag = dateFlag
        };
    }
}