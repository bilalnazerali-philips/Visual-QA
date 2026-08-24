using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
namespace VisualQa.WpfCapture;
public sealed class WpfScreenshotRenderer { public void Render(FrameworkElement element,string outputPath,int width,int height,double dpi=96) { element.Width=width;element.Height=height;element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();var bitmap=new RenderTargetBitmap(width,height,dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(element);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(outputPath);encoder.Save(stream); } }
