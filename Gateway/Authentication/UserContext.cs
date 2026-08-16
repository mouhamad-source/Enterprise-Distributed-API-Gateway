namespace Gateway.Authentication; 

public class UserContext
{
    public string UserId {get ; set; } = string.Empty; 
    public string Role {get ; set;} = "User"; 
    public string Plan{get  ;set;} = "Free"; 
    public Dictionary<string , object> Claims{get ; set; } = new(); 
}