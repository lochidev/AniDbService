namespace AniDbService.Models;

public class CalenderItem
{
    public int Id { get; set; }
    public long UnixTimeInSeconds { get; set; }
    public int DateFlag { get; set; }
}

public enum DateType
{
    StartDateUnknownDay = 0,
    StartDateUnknownMonthAndDay = 1,
    EndDateUnknownDay = 2,
    EndDateUnknownMonthAndDay = 3,
    AirDatePast = 4,
    StartDateUnknownYear = 5,
    EndDateUnknownYear = 6
}