using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Webhook.Navferty;

namespace Webhook.Navferty.Pages;

public class ConfigureResponsesModel : PageModel
{
    private readonly ResponseRepository _responseRepository;

    public ConfigureResponsesModel(ResponseRepository responseRepository)
    {
        _responseRepository = responseRepository;
    }

    [BindProperty]
    public List<ResponseModel> Responses { get; set; } = new();

    [BindProperty]
    public string NewPath { get; set; } = "/";

    [BindProperty]
    public string NewBody { get; set; } = "{}";

    public Guid TenantId { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("TenantId is required");
        }

        TenantId = tenantId;
        Responses = (await _responseRepository.FindResponses(tenantId)).ToList();
        return Page();
    }

    //public async Task<IActionResult> OnPostSaveResponseAsync(Guid tenantId, string path, string body)
    //{
    //    await _responseRepository.ConfigureResponse(tenantId, path, body);
    //    return RedirectToPage(new { tenantId });
    //}

    //public async Task<IActionResult> OnPostDuplicateResponseAsync(Guid tenantId, string path, string body)
    //{
    //    var newResponse = new ResponseModel
    //    {
    //        TenantId = tenantId,
    //        Path = path,
    //        Body = body,
    //        LastModifiedAt = DateTimeOffset.UtcNow
    //    };
    //    await _responseRepository.ConfigureResponse(newResponse.TenantId, newResponse.Path, newResponse.Body);
    //    return RedirectToPage(new { tenantId });
    //}

    public async Task<IActionResult> OnPostRemoveResponseAsync(Guid tenantId, string path)
    {
        await _responseRepository.DeleteResponse(tenantId, path);
        return RedirectToPage(new { tenantId });
    }

    //public async Task<IActionResult> OnPostAddNewResponseAsync(Guid tenantId)
    //{
    //    var newResponse = new ResponseModel
    //    {
    //        TenantId = tenantId,
    //        Path = NewPath,
    //        Body = NewBody,
    //        LastModifiedAt = DateTimeOffset.UtcNow
    //    };
    //    await _responseRepository.ConfigureResponse(newResponse.TenantId, newResponse.Path, newResponse.Body);
    //    return RedirectToPage(new { tenantId });
    //}
}
