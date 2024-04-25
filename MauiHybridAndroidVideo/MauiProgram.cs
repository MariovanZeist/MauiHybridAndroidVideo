using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;
#if ANDROID
using MauiHybridAndroidVideo.Android;
#endif

namespace MauiHybridAndroidVideo;
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

#if ANDROID
		//BlazorWebViewHandler.BlazorWebViewMapper.ModifyMapping(nameof(IBlazorWebView), (handler, view, args) =>
		//{
		//	handler.PlatformView.SetWebChromeClient(new BlazorWebChromeClient(handler));
		//});
#endif



#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
