using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AmbientContextTest.Pages;

public class SetupContextModel : PageModel
{
    private readonly AmbientContextService _contextService;
    
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }
    
    [BindProperty]
    public int? Hid { get; set; }
    
    [BindProperty]
    public int? Pid { get; set; }

    public SetupContextModel(AmbientContextService contextService)
    {
        _contextService = contextService;
    }
    
    public async Task<IActionResult> OnGet()
    {
        // Load existing context values if any
        var context = await _contextService.GetContextAsync(HttpContext);
        Hid = context.Hid;
        Pid = context.Pid;
        
        
        return Page();
    }
    
    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }
        
        // Update the context with provided values
        var context = new AmbientContext(AmbientContextSource.None, Hid, Pid);
        await _contextService.SetContextAsync(HttpContext, context);
        
        // Redirect back to the originally requested page
        return Redirect(string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl);
    }

    public async Task<IActionResult> OnPostClear()
    {
        await _contextService.ClearContextAsync(HttpContext);
        return RedirectToPage("/Index");
    }
}