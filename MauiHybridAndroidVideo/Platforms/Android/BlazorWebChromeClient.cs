using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AndroidX.Core.View;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Maui.Platform;
using File = Java.IO.File;
using Uri = Android.Net.Uri;
using View = Android.Views.View;
using WebView = Android.Webkit.WebView;

namespace MauiHybridAndroidVideo.Android;

internal class BlazorWebChromeClient : WebChromeClient
{
	WeakReference<Activity> _activityRef;
	View? _customView;
	ICustomViewCallback? _videoViewCallback;
	int _defaultSystemUiVisibility;
	bool _isSystemBarVisible;

	//The code is based on the pull request by jsuarezruiz https://github.com/dotnet/maui/pull/15472


	public BlazorWebChromeClient(BlazorWebViewHandler webViewHandler)
	{
		var activity = (webViewHandler?.MauiContext?.Context?.GetActivity()) ?? Platform.CurrentActivity;
		_activityRef = new WeakReference<Activity>(activity);
	}

	public override void OnShowCustomView(View? view, ICustomViewCallback? callback)
	{
		if (_customView is not null)
		{
			OnHideCustomView();
			return;
		}

		_activityRef.TryGetTarget(out Activity context);

		if (context is null)
			return;

		_videoViewCallback = callback;
		_customView = view;
		context.RequestedOrientation = global::Android.Content.PM.ScreenOrientation.Landscape;

		// Hide the SystemBars and Status bar
		if (OperatingSystem.IsAndroidVersionAtLeast(30))
		{
			context.Window.SetDecorFitsSystemWindows(false);

			var windowInsets = context.Window.DecorView.RootWindowInsets;
			_isSystemBarVisible = windowInsets.IsVisible(WindowInsetsCompat.Type.NavigationBars()) || windowInsets.IsVisible(WindowInsetsCompat.Type.StatusBars());

			if (_isSystemBarVisible)
				context.Window.InsetsController?.Hide(WindowInsets.Type.SystemBars());
		}
		else
		{
#pragma warning disable CS0618 // Type or member is obsolete
			_defaultSystemUiVisibility = (int)context.Window.DecorView.SystemUiVisibility;
			int systemUiVisibility = _defaultSystemUiVisibility | (int)SystemUiFlags.LayoutStable | (int)SystemUiFlags.LayoutHideNavigation | (int)SystemUiFlags.LayoutHideNavigation |
				(int)SystemUiFlags.LayoutFullscreen | (int)SystemUiFlags.HideNavigation | (int)SystemUiFlags.Fullscreen | (int)SystemUiFlags.Immersive;
			context.Window.DecorView.SystemUiVisibility = (StatusBarVisibility)systemUiVisibility;
#pragma warning restore CS0618 // Type or member is obsolete
		}

		// Add the CustomView
		if (context.Window.DecorView is FrameLayout layout)
			layout.AddView(_customView, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
	}

	public override void OnHideCustomView()
	{
		_activityRef.TryGetTarget(out Activity context);

		if (context is null)
			return;

		context.RequestedOrientation = global::Android.Content.PM.ScreenOrientation.Portrait;

		// Remove the CustomView
		if (context.Window.DecorView is FrameLayout layout)
			layout.RemoveView(_customView);

		// Show again the SystemBars and Status bar
		if (OperatingSystem.IsAndroidVersionAtLeast(30))
		{
			context.Window.SetDecorFitsSystemWindows(true);

			if (_isSystemBarVisible)
				context.Window.InsetsController?.Show(WindowInsets.Type.SystemBars());
		}
		else
#pragma warning disable CS0618 // Type or member is obsolete
			context.Window.DecorView.SystemUiVisibility = (StatusBarVisibility)_defaultSystemUiVisibility;
#pragma warning restore CS0618 // Type or member is obsolete

		_videoViewCallback?.OnCustomViewHidden();
		_customView = null;
		_videoViewCallback = null;
	}

	//Below is code that is copied from https://github.com/dotnet/maui/blob/main/src/BlazorWebView/src/Maui/Android/BlazorWebChromeClient.cs
	//As the class is internal I cannot override it.

	public override bool OnCreateWindow(WebView? view, bool isDialog, bool isUserGesture, Message? resultMsg)
	{
		if (view?.Context is not null)
		{
			// Intercept _blank target <a> tags to always open in device browser
			// regardless of UrlLoadingStrategy.OpenInWebview
			var requestUrl = view.GetHitTestResult().Extra;
			var intent = new Intent(Intent.ActionView, Uri.Parse(requestUrl));
			view.Context.StartActivity(intent);
		}
		// We don't actually want to create a new WebView window so we just return false 
		return false;
	}


	public override bool OnShowFileChooser(WebView? view, IValueCallback? filePathCallback, FileChooserParams? fileChooserParams)
	{
		if (filePathCallback is null)
		{
			return base.OnShowFileChooser(view, filePathCallback, fileChooserParams);
		}
		InternalCallFilePickerAsync(filePathCallback, fileChooserParams);
		return true;
	}

	public static async void InternalCallFilePickerAsync(IValueCallback filePathCallback, FileChooserParams? fileChooserParams)
	{
		try
		{
			await CallFilePickerAsync(filePathCallback, fileChooserParams).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
#if DEBUG
			throw;
#endif
		}
	}

	private static async Task CallFilePickerAsync(IValueCallback filePathCallback, FileChooserParams? fileChooserParams)
	{
		var pickOptions = GetPickOptions(fileChooserParams);
		var fileResults = fileChooserParams?.Mode == ChromeFileChooserMode.OpenMultiple ?
				await FilePicker.PickMultipleAsync(pickOptions) :
				new[] { (await FilePicker.PickAsync(pickOptions))! };

		if (fileResults?.All(f => f is null) ?? true)
		{
			// Task was cancelled, return null to original callback
			filePathCallback.OnReceiveValue(null);
			return;
		}

		var fileUris = new List<Uri>(fileResults.Count());
		foreach (var fileResult in fileResults)
		{
			if (fileResult is null)
			{
				continue;
			}

			var javaFile = new File(fileResult.FullPath);
			var androidUri = Uri.FromFile(javaFile);

			if (androidUri is not null)
			{
				fileUris.Add(androidUri);
			}
		}

		filePathCallback.OnReceiveValue(fileUris.ToArray());
		return;
	}


	private static PickOptions? GetPickOptions(FileChooserParams? fileChooserParams)
	{
		var acceptedFileTypes = fileChooserParams?.GetAcceptTypes();
		if (acceptedFileTypes is null ||
			(acceptedFileTypes.Length == 1 && string.IsNullOrEmpty(acceptedFileTypes[0])))
		{
			return null;
		}

		var pickOptions = new PickOptions()
		{
			FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
				{
					{ DevicePlatform.Android, acceptedFileTypes }
				})
		};
		return pickOptions;
	}
}
