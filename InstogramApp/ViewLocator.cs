using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using InstogramApp.ViewModels;

namespace InstogramApp;

[RequiresUnreferencedCode("Uses reflection for view resolution.")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null) return null;

        var vmName   = param.GetType().FullName!;
        var asmName  = param.GetType().Assembly.GetName().Name!;
        var viewName = vmName
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        // Include assembly name so Type.GetType resolves across the same assembly
        var fullTypeName = $"{viewName}, {asmName}";
        var type = Type.GetType(fullTypeName);
        if (type != null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "View not found: " + viewName };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
