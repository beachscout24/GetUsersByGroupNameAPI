namespace GetUsersByGroupName.Models;

internal class Payload
{
    public List<string?>? upn { get; set; }

    public List<string?>? users { get; set; }
}