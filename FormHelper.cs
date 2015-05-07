namespace Alienlab.NetExtensions
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Net;
  using System.Text;

  public static class FormHelper
  {
    static FormHelper()
    {
      ServicePointManager.DefaultConnectionLimit = int.MaxValue;
      ServicePointManager.ServerCertificateValidationCallback += (a, b, c, d) => true;
    }

    public static CookieContainer SubmitAndGetCookies([NotNull] Uri formUri, [NotNull] string eventTarget, [NotNull] string eventArgument, Dictionary<string, string> fields = null)
    {
      Assert.ArgumentNotNull(formUri, "formUrl");
      Assert.ArgumentNotNull(eventTarget, "eventTarget");
      Assert.ArgumentNotNull(eventArgument, "eventArgument");

      //get variables
      var html = GetHtml(formUri);
      var viewState = GetFormTag(@"__VIEWSTATE", html);
      var eventValidation = GetFormTag(@"__EVENTVALIDATION", html);

      // get auth cookie
      var postRequest = CreatePostRequest(formUri);
      var cookieContainer = new CookieContainer();
      postRequest.CookieContainer = cookieContainer;
      var sb = new StringBuilder();
      sb.Append(string.Format(@"__EVENTTARGET={0}&__EVENTARGUMENT={1}&__VIEWSTATE={2}&__EVENTVALIDATION={3}", Uri.EscapeDataString(eventTarget), Uri.EscapeDataString(eventArgument), Uri.EscapeDataString(viewState), Uri.EscapeDataString(eventValidation)));
      if (fields != null)
      {
        foreach (var field in fields)
        {
          sb.Append("&");
          sb.Append(Uri.EscapeDataString(field.Key));
          sb.Append("=");
          sb.Append(Uri.EscapeDataString(field.Value));
        }
      }

      postRequest.ContentLength = sb.Length;
      using (var sw = new StreamWriter(postRequest.GetRequestStream()))
      {
        sw.Write(sb);
      }

      using (postRequest.GetResponse())
      {
        return cookieContainer;
      }
    }

    public static HttpWebRequest CreatePostRequest(Uri formUri)
    {
      var request = CreateRequest(formUri);

      request.ProtocolVersion = new Version(1, 1);
      request.Method = "POST";
      request.ContentType = "application/x-www-form-urlencoded";
      request.Headers["Origin"] = formUri.ToString().Substring(0, formUri.ToString().IndexOf("/", "https://a".Length));
      request.Referer = formUri.ToString();

      return request;
    }

    public static HttpWebRequest CreateRequest(Uri formUri)
    {
      Assert.ArgumentNotNull(formUri, "formUri");

      var request = (HttpWebRequest)WebRequest.Create(formUri);
      Assert.IsNotNull(request, "request");

      var proxy = request.Proxy;
      Assert.IsNotNull(proxy, "proxy");

      proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
      request.Headers[HttpRequestHeader.CacheControl] = "max-age=0";
      request.Host = formUri.Host;
      request.Accept = @"text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
      request.KeepAlive = true;
      request.Headers["Accept-Encoding"] = @"gzip, deflate";
      request.Headers["Accept-Language"] = @"en-US,en;q=0.8,ru;q=0.6,uk;q=0.4";
      request.UserAgent = @"Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/40.0.2214.93 Safari/537.36";

      return request;
    }

    [NotNull]
    private static string GetFormTag(string formTag, string html)
    {
      Assert.ArgumentNotNullOrEmpty(formTag, "formTag");
      Assert.ArgumentNotNullOrEmpty(html, "html");

      const string Quote = "\"";
      var pos = html.IndexOf(Quote + formTag + Quote, StringComparison.Ordinal); // "formTag" position
      if (pos < 0)
      {
        throw new InvalidOperationException("bad html");
      }

      const string Input = "<input";
      pos = html.LastIndexOf(Input, pos, StringComparison.Ordinal); // <input position
      if (pos < 0)
      {
        throw new InvalidOperationException("bad html");
      }

      pos = html.IndexOf("value", pos + Input.Length, StringComparison.Ordinal); // value position
      if (pos < 0)
      {
        throw new InvalidOperationException("bad html");
      }

      pos = html.IndexOf("=", pos, StringComparison.Ordinal); // = position
      if (pos < 0)
      {
        throw new InvalidOperationException("bad html");
      }

      pos = html.IndexOf(Quote, pos, StringComparison.Ordinal);
      if (pos < 0)
      {
        throw new InvalidOperationException("bad html");
      }

      pos = pos + 1;
      var endPos = html.IndexOf(Quote, pos, StringComparison.Ordinal);
      if (endPos < 0)
      {
        throw new InvalidOperationException("bad html");
      }

      var len = endPos - pos;
      if (len <= 0)
      {
        throw new InvalidOperationException("bad html");
      }

      return html.Substring(pos, len);
    }

    [NotNull]
    public static string GetHtml(Uri formUri)
    {
      Assert.IsNotNull(formUri, "formUri");

      var w = (HttpWebRequest)WebRequest.Create(formUri);
      var resp = w.GetResponse();
      var html = GetHtml(resp);

      return html;
    }

    [NotNull]
    public static string GetHtml(WebResponse response)
    {
      Assert.ArgumentNotNull(response, "response");

      var responseStream = response.GetResponseStream();
      if (responseStream == null)
      {
        throw new InvalidOperationException("No response stream provided by remote server.");
      }

      using (var streamReader = new StreamReader(responseStream))
      {
        return streamReader.ReadToEnd();
      }
    }
  }
}
