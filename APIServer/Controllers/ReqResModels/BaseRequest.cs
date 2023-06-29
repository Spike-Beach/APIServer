using System.ComponentModel.DataAnnotations;

namespace APIServer.Controllers.ReqResModels;

public class BaseRequest
{
    [Required] public String userAssignedId { get; set; }
    [Required] public String token { get; set; }
    [Required] public String clientVersion { get; set;}
}
