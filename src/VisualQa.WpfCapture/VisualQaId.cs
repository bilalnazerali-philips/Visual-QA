using System.Windows;
namespace VisualQa.WpfCapture;
public static class VisualQa { public static readonly DependencyProperty IdProperty=DependencyProperty.RegisterAttached("Id",typeof(string),typeof(VisualQa),new PropertyMetadata("")); public static void SetId(DependencyObject o,string value)=>o.SetValue(IdProperty,value); public static string GetId(DependencyObject o)=>(string)o.GetValue(IdProperty); }
