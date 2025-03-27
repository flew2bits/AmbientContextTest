using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AmbientContextTest.Pages;

[RequireAmbientContext]
public class SecurePage : PageModel
{
    [FromAmbientContext("Hid")]
    public int HouseholdId { get; set; }
    
    [FromAmbientContext("Pid")]
    public int PersonId { get; set; }
    
    
    private readonly AmbientContextService _contextService;

    public SecurePage(AmbientContextService contextService)
    {
        _contextService = contextService;
    }
    
    public async Task<IActionResult> OnGet()
    {
        // The ambient context is available in HttpContext.Items
        var context = (AmbientContext)HttpContext.Items["AmbientContext"];
        
        // Use context.Hid and context.Pid values
        ViewData["Hid"] = context.Hid;
        ViewData["Pid"] = context.Pid;
        
        return Page();
    }
}