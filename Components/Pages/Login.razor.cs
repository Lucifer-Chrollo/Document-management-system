using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;

namespace DocumentManagementSystem.Components.Pages
{
    public partial class Login
    {
[SupplyParameterFromQuery(Name = "error")]
    public string? ErrorMessage { get; set; }
    
    [SupplyParameterFromQuery(Name = "message")]
    public string? SuccessMessage { get; set; }

    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrl { get; set; } = "/";
    }
}
