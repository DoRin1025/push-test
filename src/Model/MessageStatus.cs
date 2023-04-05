using Microsoft.AspNetCore.Mvc;

namespace Model
{
    public class MessageStatus
    {
        [BindProperty(Name = "id")] public string Id { get; set; }
    }
}