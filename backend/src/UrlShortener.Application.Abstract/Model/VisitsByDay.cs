namespace UrlShortener.Application.Abstract.Model;

public sealed record VisitsByDay(DateOnly Date, int Count);
