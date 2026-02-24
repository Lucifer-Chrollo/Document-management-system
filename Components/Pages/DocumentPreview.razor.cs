using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using DocumentManagementSystem.Models;

namespace DocumentManagementSystem.Components.Pages
{
    public partial class DocumentPreview
    {
[Parameter]
    public int DocumentId { get; set; }
    }
}
