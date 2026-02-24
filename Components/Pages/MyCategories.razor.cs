using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Radzen;
using Microsoft.JSInterop;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;

namespace DocumentManagementSystem.Components.Pages
{
    public partial class MyCategories
    {
private IEnumerable<Category> categories = Enumerable.Empty<Category>();
    private bool isLoading = true;
    private int currentUserId;
    private bool isAdmin;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        isAdmin = user.Identity?.Name?.ToLower() == "admin";

        var idClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (idClaim != null && int.TryParse(idClaim.Value, out var id))
        {
            currentUserId = id;
        }

        await LoadCategories();
    }

    private async Task LoadCategories()
    {
        isLoading = true;
        try
        {
            if (isAdmin)
                categories = await CategoryService.GetCategoriesAsync();
            else
                categories = await CategoryService.GetCategoriesByUserAsync(currentUserId);
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", ex.Message);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task OnCreateCategory()
    {
        var name = await DialogService.OpenAsync<CategoryNameDialog>("Create New Category",
            new Dictionary<string, object>(),
            new DialogOptions { Width = "400px" });

        if (name is string categoryName && !string.IsNullOrWhiteSpace(categoryName))
        {
            var cat = new Category
            {
                CategoryName = categoryName,
                CreatedBy = currentUserId
            };
            var result = await CategoryService.CreateCategoryAsync(cat);
            if (result.Result)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Created", $"Category '{categoryName}' created.");
                await LoadCategories();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", result.Message);
            }
        }
    }

    private async Task OnEditCategory(Category cat)
    {
        var name = await DialogService.OpenAsync<CategoryNameDialog>("Rename Category",
            new Dictionary<string, object> { { "InitialName", cat.CategoryName } },
            new DialogOptions { Width = "400px" });

        if (name is string newName && !string.IsNullOrWhiteSpace(newName))
        {
            cat.CategoryName = newName;
            var result = await CategoryService.UpdateCategoryAsync(cat);
            if (result.Result)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Updated", $"Category renamed to '{newName}'.");
                await LoadCategories();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", result.Message);
            }
        }
    }

    private async Task OnDeleteCategory(Category cat)
    {
        var confirm = await DialogService.Confirm(
            $"Delete category '{cat.CategoryName}'? Documents in this category won't be deleted.",
            "Confirm Delete",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirm == true)
        {
            var result = await CategoryService.DeleteCategoryAsync(cat.CategoryId);
            if (result.Result)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Deleted", $"Category '{cat.CategoryName}' deleted.");
                await LoadCategories();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", result.Message);
            }
        }
    }
    }
}
