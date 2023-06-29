using System.ComponentModel.DataAnnotations;

namespace APIServer.Controllers.ReqResModels;

public class UserInfoRequest : BaseRequest
{
    [Required] public String userAssignedId { get; set; }
    [Required] public String token { get; set; }
    [Required] public String clientVersion { get; set; }
}
public class UserInfoResponse : BaseResponse 
{
    public Int32 winCnt { get; set; }
    public Int32 loseCnt { get; set; }
}

