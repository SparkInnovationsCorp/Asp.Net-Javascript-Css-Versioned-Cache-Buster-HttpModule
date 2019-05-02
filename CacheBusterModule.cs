using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace CacheBusterDemo
{
    /// <summary>
    /// -------------------------------------------------------------
    /// The HttpModule finds script and link (css) tags, and appends the 
    /// assemble version number on the src and href respectivly.  This 
    /// allows us to control cacheing by assembly version.  When creating 
    /// a new release, increase the assembly version to gaurantee javascript 
    /// files are pulled.
    /// -------------------------------------------------------------
    /// Created by Jason Bramble
    /// Created on January 5, 2019
    /// </summary>
    public class CacheBusterModule : IHttpModule
    {
        string[] _validExtensions = new string[] { "", ".htm", ".html" }; 

        void IHttpModule.Dispose()
        {
        }

        void IHttpModule.Init(HttpApplication context)
        {
            context.BeginRequest += (o, e) => {
                //The script below does filter on content type, but why parse if we really don't need to?
                //This just adds an extra layer to save unneeded work.
                if (_validExtensions.Contains(context.Request.CurrentExecutionFilePathExtension.ToLower()))
                {
                    context.Response.Filter = new CacheBusterStreamFilter(context.Response, context.Response.Filter);
                }
            };
        }
    }

    /// <summary>
    /// Filters on specific tags to add versioning information
    /// </summary>
    public class CacheBusterStreamFilter : Stream
    {
        private Stream _base;
        private HttpResponse _response;
        private MemoryStream _memoryBase = new MemoryStream();
        private string _version;
        private int _size = 0;

        #region "We do not use this part of the interface"

        public override bool CanRead => throw new NotImplementedException();

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public override long Length => throw new NotImplementedException();

        #endregion

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public CacheBusterStreamFilter(HttpResponse response, Stream stream)
        {
            _base = stream;
            _response = response;

            //Version of current assembly
            _version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _memoryBase.Write(buffer, offset, count);
            _size += count;
        }

        public override void Flush()
        {
            //lets only work on text/html types.  do not change others.
            if (_response.ContentType == "text/html")
            {
                // Get static HTML code
                string html = Encoding.UTF8.GetString(_memoryBase.GetBuffer());

                //handles script tags
                html = AddVersionTo(html, "<script[\\s\\S]*?>", "src");

                //handles link tags
                html = AddVersionTo(html, "<link[\\s\\S]*?>", "href");

                // Flush modified HTML
                byte[] buffer = Encoding.UTF8.GetBytes(html);
                _base.Write(buffer, 0, buffer.Length);
            }
            else
            {
                //Some other type we don't filter
                _memoryBase.Flush();

                byte[] buffer = _memoryBase.GetBuffer();
                _base.Write(buffer, 0, _size);
            }

            _base.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        private string AddVersionTo(string html, string regExPattern, string attribute)
        {
            // Handle all javascript refrences
            Regex regex = new Regex(regExPattern, RegexOptions.IgnoreCase);
            Match match = regex.Match(html);
            while (match.Success)
            {
                string oldValue = match.Value;
                string newValue = AddVersionToAttribute(oldValue, attribute);
                html = html.Replace(oldValue, newValue);
                match = match.NextMatch();
            }

            return html;
        }

        private string AddVersionToAttribute(string value, string attribute)
        {
            string prefix = "?";

            int index1 = value.IndexOf(" " + attribute, StringComparison.CurrentCultureIgnoreCase);

            if (index1 > -1)
                index1 = index1 + +(attribute.Length + 1);

            if (index1 > -1)
                index1 = value.IndexOf("=", index1);

            string schar = "";
            if (index1 > -1)
            {
                int cindex = value.IndexOf("'", index1 + 1);

                if (cindex == -1)
                {
                    cindex = value.IndexOf("\"", index1 + 1);
                    if (cindex != -1) schar = "\"";
                }
                else
                    schar = "'";

                index1 = cindex;
            }

            if (index1 > -1)
            {
                int index2 = value.IndexOf(schar, index1 + 1);

                if (index2 > -1)
                {
                    string link = value.Substring(index1 + 1, index2 - (index1 + 1));
                    if (link.Contains("?")) prefix = "&";
                }

                index1 = index2;
            }

            if (index1 > -1)
                return value.Substring(0, index1) + prefix + "v=" + _version + value.Substring(index1);
            else
                return value;

        }
    }

}
