namespace Gateway.Configuration; 

public class RateLimitingConfig
{
    public int Limit {get ;set ; } = 100 ; 
    public int WindowSeconds {get ; set;} = 60 ;
}