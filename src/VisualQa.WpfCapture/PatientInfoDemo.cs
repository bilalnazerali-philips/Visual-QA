using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace VisualQa.WpfCapture;
public static class PatientInfoDemo { public static FrameworkElement Create(){var avatar=new Border { Width=48,Height=48,Margin=new Thickness(16,12,8,12),Background=Brushes.SteelBlue,Child=new TextBlock { Text="AB",Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center }};VisualQa.SetId(avatar,"patient-avatar");var name=new TextBlock { Text="Avery Brooks",FontSize=16,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center };VisualQa.SetId(name,"patient-name");return new StackPanel { Width=300,Height=72,Background=Brushes.White,Orientation=Orientation.Horizontal,Children={avatar,name}};} }
