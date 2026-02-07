namespace APIGateWay.ModalLayer
{
   
 
        public class UserInfo
        {
            public Guid UserId { get; set; }
            public string? UserName { get; set; }
            //public string? ClientId { get; set; }
            public string? DBName { get; set; }
            public string? Status { get; set; }
            //public string? Key { get; set; }
            public string? JwtToken { get; set; }
            public int? Role { get; set; }


        }
    
}
