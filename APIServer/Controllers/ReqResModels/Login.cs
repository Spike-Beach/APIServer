using System.ComponentModel.DataAnnotations;

namespace APIServer.Controllers.ReqResModels
{
    public class LoginRequest
    {
        [Required] public String userAssignedId { get; set; }

        [Required]
        [DataType(DataType.Password)] //데이터어노테이션
        public String password { get; set; }
        [Required] public String clientVersion { get; set; }
    }

    public class LoginResponse : BaseResponse
    {
        public String token { get; set; }
    }
}
