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
    public partial class Counter
    {
private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
    }
}
