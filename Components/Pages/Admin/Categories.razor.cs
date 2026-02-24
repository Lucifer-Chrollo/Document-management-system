using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using Radzen;
using Radzen.Blazor;
using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;

namespace DocumentManagementSystem.Components.Pages.Admin
{
    public partial class Categories
    {
RadzenDataGrid<Category> grid = default!;
    IEnumerable<Category>? categories;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        categories = await CategoryService.GetCategoriesAsync();
    }

    private async Task OnCreateCategory()
    {
        var result = await DialogService.OpenAsync<CreateCategoryDialog>("Create New Category");

        if (result != null)
        {
            var newCategory = new Category { CategoryName = (string)result };
            var response = await CategoryService.CreateCategoryAsync(newCategory);

            if (response.Result)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success", "Category created successfully");
                await LoadData();
                await grid.Reload();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", response.Message);
            }
        }
    }

    private async Task OnEditCategory(Category category)
    {
        var result = await DialogService.OpenAsync<CreateCategoryDialog>("Edit Category", 
            new Dictionary<string, object> { { "categoryName", category.CategoryName } });

        if (result != null)
        {
            category.CategoryName = (string)result;
            var response = await CategoryService.UpdateCategoryAsync(category);

            if (response.Result)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success", "Category updated successfully");
                await LoadData();
                await grid.Reload();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", response.Message);
            }
        }
    }

    private async Task OnDeleteCategory(Category category)
    {
        var confirm = await DialogService.Confirm($"Are you sure you want to delete category '{category.CategoryName}'?", "Confirm Delete");

        if (confirm == true)
        {
            var response = await CategoryService.DeleteCategoryAsync(category.CategoryId);

            if (response.Result)
            {
                NotificationService.Notify(NotificationSeverity.Success, "Success", "Category deleted successfully");
                await LoadData();
                await grid.Reload();
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Error", response.Message);
            }
        }
    }
    }
}
