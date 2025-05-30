﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AmbientContextTest.Pages;

[RequireAmbientContext("Partial")]
public class PartialContextPage : PageModel
{
    private readonly AmbientContextService _contextService;
    
    [FromAmbientContext(nameof(AmbientContext.Pid))]
    public int? PersonId { get; set; }
    
    [FromAmbientContext(nameof(AmbientContext.Hid))]
    public int? HouseholdId { get; set; }

    public PartialContextPage(AmbientContextService contextService)
    {
        _contextService = contextService;
    }
    
    public async Task<IActionResult> OnGet()
    {
        // The ambient context is available, might be partial

        
        return Page();
    }
}
