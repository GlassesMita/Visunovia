using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Visunovia.Engine.Plugin;

namespace Visunovia.Controls;

public partial class PluginPanel : UserControl
{
    private PluginManager? _pluginManager;
    private readonly List<AssemblyInfo> _assemblies = new();

    public PluginPanel()
    {
        InitializeComponent();
    }

    public void Initialize(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
        RefreshAssemblies();
    }

    public void RefreshAssemblies()
    {
        AssemblyComboBox.Items.Clear();
        ClassComboBox.Items.Clear();
        MethodTextBox.Text = "";
        _assemblies.Clear();

        if (_pluginManager == null) return;

        foreach (var assembly in _pluginManager.Assemblies)
        {
            AssemblyComboBox.Items.Add(assembly.Name);
            _assemblies.Add(assembly);
        }
    }

    private void OnAssemblyChanged(object sender, SelectionChangedEventArgs e)
    {
        ClassComboBox.Items.Clear();
        MethodTextBox.Text = "";

        var index = AssemblyComboBox.SelectedIndex;
        if (index < 0 || index >= _assemblies.Count) return;

        var assembly = _assemblies[index];
        foreach (var type in assembly.Types)
        {
            ClassComboBox.Items.Add(type.FullName);
        }
    }

    private void OnClassChanged(object sender, SelectionChangedEventArgs e)
    {
        MethodTextBox.Text = "";

        var assemblyIndex = AssemblyComboBox.SelectedIndex;
        if (assemblyIndex < 0 || assemblyIndex >= _assemblies.Count) return;
    }

    private void OnInvokeClicked(object sender, RoutedEventArgs e)
    {
        if (_pluginManager == null) return;

        var assemblyName = AssemblyComboBox.SelectedItem as string;
        var className = ClassComboBox.SelectedItem as string;
        var methodName = MethodTextBox.Text.Trim();

        if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(className) || string.IsNullOrEmpty(methodName))
        {
            MessageBox.Show("请选择程序集、类并输入方法名。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var success = _pluginManager.InvokeMethod(assemblyName, className, methodName);
        if (success)
        {
            MessageBox.Show($"方法 {methodName} 执行成功。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show($"方法 {methodName} 执行失败。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
