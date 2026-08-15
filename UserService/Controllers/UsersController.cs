using Microsoft.AspNetCore.Mvc; 
using UserService.Modles; 

namespace UserService.Controllers; 

[ApiController]
[Route("users")]
public class UserController : ControllerBase
{
    private static readonly List<User> _users = new()
    {
        new User{Id = 1 , Name = "sousou"}, 
        new User{Id =2 , Name = "mouhamad"}, 
        new User{Id = 3 , Name ="sondos"}
    }; 

    [HttpGet]
    public IActionResult GetAll() => Ok(_users); 

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var user = _users.FirstOrDefault(u => u.Id == id); 
        if(user is null)
            return NotFound($"User with id {id} not found."); 
        return Ok(user);     
    } 

}