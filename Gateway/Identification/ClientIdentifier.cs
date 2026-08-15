namespace Gateway.Identification; 

public class ClientIdentifier
{
    public string Type {get ;}
    public string Value {get; }

    public ClientIdentifier(String type , string value)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type)); 
        Value = value ?? throw new ArgumentNullException(nameof(value)); 
    }
    public string ToRedisKey(string prefix = "RateLimit:")
    {
        return $"{prefix}{Type}:{Value}"; 
    }
    
}