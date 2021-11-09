namespace AniDbService.Models;

public class CalenderItem
{
    public int Id { get; init; }
    public long UnixTimeInSeconds { get; init; }
    public int DateFlag { get; init; }
}

// public enum DateType
// {
//     StartDateUnknownDay = 0,
//     StartDateUnknownMonthAndDay = 1,
//     EndDateUnknownDay = 2,
//     EndDateUnknownMonthAndDay = 3,
//     AirDatePast = 4,
//     StartDateUnknownYear = 5,
//     EndDateUnknownYear = 6
// }