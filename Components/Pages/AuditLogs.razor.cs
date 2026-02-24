using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;


namespace DocumentManagementSystem.Components.Pages
{
    public partial class AuditLogs
    {
private IEnumerable<AuditLog> logs = Enumerable.Empty<AuditLog>();
    private AuditStats stats = new();
    private bool isLoading = true;
    
    // Filters
    private string? filterAction;
    private DateTime? filterFromDate;
    private DateTime? filterToDate;
    
    private readonly string[] actionTypes = new[] 
    { 
        AuditAction.View, AuditAction.Download, AuditAction.Create, 
        AuditAction.Update, AuditAction.Delete, AuditAction.Restore,
        AuditAction.Login, AuditAction.Logout
    };

    protected override async Task OnInitializedAsync()
    {
        await LoadLogs();
    }

    private async Task LoadLogs()
    {
        isLoading = true;
        StateHasChanged();

        try
        {
            logs = await AuditService.GetLogsAsync(
                action: filterAction,
                fromDate: filterFromDate,
                toDate: filterToDate?.AddDays(1), // Include full day
                limit: 500
            );
            stats = await AuditService.GetStatsAsync(filterFromDate, filterToDate?.AddDays(1));
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to load audit logs.");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task ClearFilters()
    {
        filterAction = null;
        filterFromDate = null;
        filterToDate = null;
        await LoadLogs();
    }

    private static BadgeStyle GetActionBadgeStyle(string action) => action switch
    {
        AuditAction.View or AuditAction.Preview => BadgeStyle.Info,
        AuditAction.Download => BadgeStyle.Success,
        AuditAction.Create or AuditAction.VersionUpload => BadgeStyle.Primary,
        AuditAction.Update => BadgeStyle.Warning,
        AuditAction.Delete or AuditAction.PermanentDelete => BadgeStyle.Danger,
        AuditAction.Restore or AuditAction.VersionRestore => BadgeStyle.Secondary,
        AuditAction.Login => BadgeStyle.Light,
        AuditAction.Logout => BadgeStyle.Light,
        _ => BadgeStyle.Light
    };

    private static string TruncateDetails(string? details)
    {
        if (string.IsNullOrEmpty(details)) return "-";
        return details.Length > 50 ? details[..50] + "..." : details;
    }
    }
}
