using Microsoft.AspNetCore.Components;

namespace DocumentManagementSystem.Components;

/// <summary>
/// Simple display component for a Label-Value pair.
/// </summary>
public partial class PropertyRow
{
    [Parameter] public string Label { get; set; } = string.Empty;
    [Parameter] public string? Value { get; set; }
}
