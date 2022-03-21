﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Specialized;
using System.IO;
using XiaoFeng.Validator;
using System.Linq.Expressions;
#if NETCORE
using Microsoft.Extensions.Primitives;
#endif
using XiaoFeng.Web;
using HttpContext = XiaoFeng.Web.HttpContext;
#if NETFRAMEWORK
using System.Web;
#else
using Microsoft.AspNetCore.Http;
#endif

namespace XiaoFeng
{
    /// <summary>
    /// 请求数据
    /// Verstion : 1.0.6
    /// Author : jacky
    /// Email : jacky@zhuovi.com
    /// QQ : 7092734
    /// Site : www.zhuovi.com
    /// Create Time : 2018/08/31 11:30:05
    /// Update Time : 2018/09/03 18:00:16
    /// </summary>
    public class XToken
    {
#region 构造器
        /// <summary>
        /// 构造器
        /// </summary>
#if NETFRAMEWORK
        public XToken()
        {
            this.Result = new List<string>();
            this.Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.UseData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var Request = HttpContext.Current.Request;
            var nameValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Request.Headers.AllKeys.Each(a =>
            {
                if (Request.Headers[a] != null)
                    nameValue.Add(a, Request.Headers[a].ToString().UrlDecode());
            });
            Request.QueryString.AllKeys.Each(a =>
            {
                if (Request.QueryString[a] != null)
                    nameValue.Add(a, Request.QueryString[a].ToString().UrlDecode());
            });
            if (Request.HttpMethod == "POST")
            {
                if (Request.Form.AllKeys.Length > 0)
                {
                    Request.Form.AllKeys.Each(a =>
                    {
                        if (Request.Form[a] != null)
                        {
                            if (nameValue.ContainsKey(a))
                                nameValue[a] = Request.Form[a].ToString().UrlDecode();
                            else
                                nameValue.Add(a, Request.Form[a].ToString().UrlDecode());
                        }
                    });
                }
                else
                {
                    var stream = Request.InputStream;
                    if (stream != null && stream.CanRead && HttpContext.Current.Request.Files.Count == 0)
                    {
                        var _stream = new MemoryStream();
                        stream.Position = 0;
                        stream.CopyTo(_stream);
                        if (_stream.Length > 0)
                        {
                            _stream.Position = 0;
                            using (var sr = new StreamReader(_stream))
                            {
                                var body = sr.ReadToEnd();
                                if (body.IsJson())
                                {
                                    var d = body.JsonToObject<Dictionary<string, string>>();
                                    d.Each(a =>
                                    {
                                        if (!this.Data.ContainsKey(a.Key))
                                            this.Data.Add(a.Key, a.Value.UrlDecode());
                                    });
                                }
                                else if (body.IsQuery())
                                {
                                    body.GetMatches(@"(?<a>[^=&]+)=(?<b>[^&]*)").Each(a =>
                                    {
                                        var Key = a["a"];
                                        var Value = a["b"];
                                        if (!this.Data.ContainsKey(Key))
                                            this.Data.Add(Key, Value.UrlDecode());
                                    });
                                }
                            }
                        }
                    }
                    else this.Files = HttpContext.Current.Request.Files;
                }
            }
            nameValue.Keys.Each(key =>
            {
                string paramValue = nameValue[key];
                if (key == null)
                {
                    if (paramValue.IsJson())
                    {
                        paramValue.JsonToObject<Dictionary<string, string>>().Each(d =>
                        {
                            if (this.Data.ContainsKey(d.Key))
                                this.Data[d.Key] = d.Value.UrlDecode();
                            else
                                this.Data.Add(d.Key, d.Value.UrlDecode());
                        });
                    }
                    else if (paramValue.IsQuery())
                    {
                        paramValue.GetMatches(@"(?<a>[^=&]+)=(?<b>[^&]*)").Each(a =>
                        {
                            var Key = a["a"];
                            var value = a["b"];
                            if (!this.Data.ContainsKey(Key))
                                this.Data.Add(Key, value.UrlDecode());
                            else
                                this.Data[Key] = value.UrlDecode();
                        });
                    }
                }
                else
                {
                    if (this.Data.ContainsKey(key))
                        this.Data[key] = paramValue;
                    else
                        this.Data.Add(key, paramValue);
                }
            });
            string id = Request.Url.AbsolutePath.ToString().GetMatch(@"/(?<a>\d+)$");
            if (id.IsNotNullOrEmpty() && !this.Data.ContainsKey("id")) this.Data.Add("id", id.UrlDecode());
            string[] Segments = Request.Url.Segments;
            for (int i = 0; i < Segments.Length; i++)
            {
                string key = i.ToString(), val = Segments[i].Trim('/');
                if (val.IsNullOrEmpty()) val = Request.Url.Host;
                if (this.Data.ContainsKey(val))
                    this.Data[key] = val;
                else
                    this.Data.Add(key, val);
            }
        }
#else
        public XToken()
        {
            this.Result = new List<string>();
            this.Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.UseData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var Request = HttpContext.Current.Request;
            var nameValue = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            Request.Headers.Keys.Each(a =>
            {
                if (Request.Headers.TryGetValue(a, out var val) && !nameValue.ContainsKey(a))
                    nameValue.Add(a, val.ToString().UrlDecode());
            });
            Request.Query.Keys.Each(a =>
            {
                if (Request.Query.TryGetValue(a, out var val) && !nameValue.ContainsKey(a))
                    nameValue.Add(a, val.ToString().UrlDecode());
            });
            if (Request.Method == "POST")
            {
                if (Request.HasFormContentType)
                {
                    Request.Form.Keys.Each(a =>
                    {
                        if (Request.Form.TryGetValue(a, out var val))
                        {
                            if (nameValue.ContainsKey(a))
                                nameValue[a] = val.ToString().UrlDecode();
                            else
                                nameValue.Add(a, val.ToString().UrlDecode());
                        }
                    });
                }
                else
                {
                    var stream = Request.Body;
                    if (stream != null && stream.CanRead && HttpContext.Current.Request.Form.Files.Count == 0)
                    {
                        var _stream = new MemoryStream();
                        stream.Position = 0;
                        stream.CopyTo(_stream);
                        if (_stream.Length > 0)
                        {
                            _stream.Position = 0;
                            using (var sr = new StreamReader(_stream))
                            {
                                var body = sr.ReadToEnd();
                                if (body.IsJson())
                                {
                                    var d = body.JsonToObject<Dictionary<string, string>>();
                                    d.Each(a =>
                                    {
                                        if (!this.Data.ContainsKey(a.Key))
                                            this.Data.Add(a.Key, a.Value.UrlDecode());
                                    });
                                }
                                else if (body.IsQuery())
                                {
                                    body.GetMatches(@"(?<a>[^=&]+)=(?<b>[^&]*)").Each(a =>
                                    {
                                        var Key = a["a"];
                                        var Value = a["b"];
                                        if (!this.Data.ContainsKey(Key))
                                            this.Data.Add(Key, Value.UrlDecode());
                                    });
                                }
                            }
                        }
                    }
                    else this.Files = HttpContext.Current.Request.Form.Files;
                }
            }
            nameValue.Keys.Each(key =>
            {
                string paramValue = nameValue[key];
                if (key == null)
                {
                    if (paramValue.IsJson())
                    {
                        paramValue.JsonToObject<Dictionary<string, string>>().Each(d =>
                        {
                            if (this.Data.ContainsKey(d.Key))
                                this.Data[d.Key] = d.Value.UrlDecode();
                            else
                                this.Data.Add(d.Key, d.Value.UrlDecode());
                        });
                    }
                    else if (paramValue.IsQuery())
                    {
                        paramValue.GetMatches(@"(?<a>[^=&]+)=(?<b>[^&]*)").Each(a =>
                        {
                            var Key = a["a"];
                            var value = a["b"];
                            if (!this.Data.ContainsKey(Key))
                                this.Data.Add(Key, value.UrlDecode());
                            else
                                this.Data[Key] = value.UrlDecode();
                        });
                    }
                }
                else
                {
                    if (this.Data.ContainsKey(key))
                        this.Data[key] = paramValue;
                    else
                        this.Data.Add(key, paramValue);
                }
            });
            string id = Request.PathBase.ToString().GetMatch(@"/(?<a>\d+)$");
            if (id.IsNotNullOrEmpty() && !this.Data.ContainsKey("id")) this.Data.Add("id", id.UrlDecode());
            string[] Segments = Request.GetUri().Segments;
            for (int i = 0; i < Segments.Length; i++)
            {
                string key = i.ToString(), val = Segments[i].Trim('/');
                if (val.IsNullOrEmpty()) val = Request.Host.Host;
                if (this.Data.ContainsKey(val))
                    this.Data[key] = val;
                else
                    this.Data.Add(key, val);
            }
        }
#endif
#endregion

#region 属性
        /// <summary>
        /// 上传文件集
        /// </summary>
#if NETFRAMEWORK
        public HttpFileCollection Files { get; set; }
#else
        public IFormFileCollection Files { get; set; }
#endif

        /// <summary>
        /// 是否验证通过
        /// </summary>
        public Boolean IsValid { get { return this.Result.Count == 0; } }
        /// <summary>
        /// 验证结果
        /// </summary>
        public List<string> Result { get; set; } = new List<string>();
        /// <summary>
        /// 数据
        /// </summary>
        [NonSerialized]
        public Dictionary<string, string> Data;
        /// <summary>
        /// 用过的数据
        /// </summary>
        [NonSerialized]
        public Dictionary<string, string> UseData;
#endregion

#region 方法
                /// <summary>
                /// 获取值 不过滤SQL注入
                /// </summary>
                /// <param name="name">键名</param>
                /// <returns></returns>
                public string this[string name]
                {
                    get
                    {
                        if (name.IsNullOrEmpty()) return "";
                        if (!this.Data.TryGetValue(name, out string _val)) _val = "";
                        if (!this.UseData.ContainsKey(name)) this.UseData.Add(name, _val);
                        return _val;
                    }
                }
                /// <summary>
                /// 获取数据 过滤SQL注入
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="defaultValue">默认值</param>
                /// <returns></returns>
                public T Value<T>(string name = "", T defaultValue = default(T))
                {
                    ValueTypes baseType = typeof(T).GetValueType();
                    if (name.IsNullOrEmpty())
                    {
                        if (baseType == ValueTypes.Struct || baseType == ValueTypes.Class)
                        {
                            T val = this.Data.DictionaryToObject<T>();
                            object o = val;
                            val.GetType().GetMembers().Each(m =>
                            {
                                if (m.MemberType == MemberTypes.Property)
                                {
                                    PropertyInfo p = m as PropertyInfo;
                                    if (p.PropertyType != typeof(string)) return;
                                    string _val = p.GetValue(val, null).ToString();
                                    if (_val.IsNotNullOrEmpty())
                                    {
                                        _val = _val.ReplaceSQL();
                                        p.SetValue(o, _val);
                                    }
                                }
                                else if (m.MemberType == MemberTypes.Field)
                                {
                                    FieldInfo f = m as FieldInfo;
                                    if (f.FieldType != typeof(string)) return;
                                    string _val = f.GetValue(val).ToString();
                                    if (_val.IsNotNullOrEmpty())
                                    {
                                        _val = _val.ReplaceSQL();
                                        f.SetValue(o, _val);
                                    }
                                }
                            });
                            val = (T)o;
                            return val;
                        }
                        else return defaultValue;
                    }
                    else
                        return this[name].ReplaceSQL().ToCast(defaultValue);
                }
                /// <summary>
                /// 获取数据  过滤SQL注入
                /// </summary>
                /// <param name="name">名称</param>
                /// <returns></returns>
                public string Value(string name)
                {
                    if (name.IsNullOrEmpty()) return "";
                    return this[name].ReplaceSQL();
                }
                /// <summary>
                /// 获取数据并验证有效性 过滤SQL注入
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="format">验证格式</param>
                /// <returns></returns>
                public T Value<T>(string name, ValidateFormat format)
                {
                    if (!name.IsNullOrEmpty())
                    {
                        var value = this[name];
                        this.Result.AddRange(new ConditionValidator(name, value, format).Result);
                    }
                    return this.Value<T>(name);
                }
                /// <summary>
                /// 获取数据并验证有效性 过滤SQL注入
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="func">Lambda表达式</param>
                /// <returns></returns>
                public T Value<T>(string name, Expression<Func<ValidatorFormat, bool>> func)
                {
                    string value = "";
                    if (!name.IsNullOrEmpty())
                    {
                        value = this[name];
                        if (func != null)
                        {
                            string _value = func.Body.ToString().ReplacePattern(@".Equals\(", " == ");
                            List<Dictionary<string, string>> list = _value.GetMatches(@"\.(?<a>[a-z]+)(\s*==\s*(?<b>-?[\sa-z0-9\,\(""]+))?");
                            Dictionary<string, string> data = new Dictionary<string, string>();
                            list.Each(a =>
                            {
                                if (!data.ContainsKey(a["a"]))
                                {
                                    string _key = a["a"], _val = a["b"].Trim('"').RemovePattern(@"(new BetweenValue|Convert)\(");
                                    if (_key.IsMatch(@"^Is[a-z]+$"))
                                    {
                                        if (_key == "IsPattern")
                                            _val = _value.GetMatch(@"IsPattern == ""(?<a>.*?)""(\)[\s\)]?)");
                                        else
                                        {
                                            if (_value.IsMatch(@"(\s*|\()Not\([a-z]+\." + _key + @"+\)"))
                                                _val = "False";
                                            else if (_val.IsNullOrEmpty()) _val = "True";
                                        }
                                    }
                                    data.Add(_key, _val);
                                }
                            });
                            Type ValidatorType = Type.GetType("XiaoFeng.Validator.ConditionValidator");
                            Type FormatType = Type.GetType("XiaoFeng.Validator.ValidateFormat");
                            object format = Activator.CreateInstance(FormatType);
                            data.Each(d =>
                            {
                                PropertyInfo p = FormatType.GetProperty(d.Key, BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
                                string val = d.Value;
                                object o = null;
                                if (val.IsNullOrEmpty())
                                    o = null;
                                else if (d.Key == "Between")
                                {
                                    string[] _val = val.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    Type between = Type.GetType("XiaoFeng.Validator.BetweenValue");
                                    o = Activator.CreateInstance(between, new object[] { _val[0].Trim().ToDouble(), _val[1].Trim().ToDouble() });
                                }
                                else
                                {
                                    Type _t = p.PropertyType;
                                    if (_t.IsGenericType && _t.GetGenericTypeDefinition() == typeof(Nullable<>)) _t = _t.GetGenericArguments()[0];
                                    o = Convert.ChangeType(val, _t);
                                }
                                p.SetValue(format, o);
                            });
                            object Validator = Activator.CreateInstance(ValidatorType, new object[] { name, value, format });
                            PropertyInfo Result = ValidatorType.GetProperty("Result",BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);
                            object result = Result.GetValue(Validator, null);
                            this.Result.AddRange(result as List<string>);
                        }
                    }
                    return this.Value<T>(name);
                }
#endregion

#region 获取参数值
                /// <summary>
                /// 获取参数值 原始值
                /// </summary>
                /// <param name="name">名称</param>
                /// <returns></returns>
                public string get(string name)
                {
#if NETFRAMEWORK
                    return this._value(HttpContext.Current.Request.QueryString, name);
#else
                    return this._value(HttpContext.Current.Request.Query as IEnumerable<KeyValuePair<string,StringValues>>, name);
#endif
                }
                /// <summary>
                /// 获取参数值
                /// </summary>
                /// <param name="name">名称</param>
                /// <returns></returns>
                public string Get(string name)
                {
                    if (name.IsNullOrEmpty()) return HttpContext.Current.Request.QueryString.ToString().TrimStart('?');
                    return this.Get<string>(name);
                }
                /// <summary>
                /// 获取参数值
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="defaultValue">默认值</param>
                /// <returns></returns>
                public T Get<T>(string name, T defaultValue = default(T))
                {
#if NETFRAMEWORK
                    return this._Value<T>(HttpContext.Current.Request.QueryString, name, defaultValue);
#else
                    return this._Value<T>(HttpContext.Current.Request.Query as IEnumerable<KeyValuePair<string, StringValues>>, name, defaultValue);
#endif
                }
                /// <summary>
                /// 获取参数值
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="format">验证规则</param>
                /// <returns></returns>
                public T Get<T>(string name, ValidateFormat format)
                {
#if NETFRAMEWORK
                    return this._Value<T>(HttpContext.Current.Request.QueryString, name, format);
#else
                     return this._Value<T>(HttpContext.Current.Request.Query as IEnumerable<KeyValuePair<string, StringValues>>, name, format);
#endif
                }
                /// <summary>
                /// 获取参数值 验证数据有效性 过滤SQL注入
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="func">Lambda表达式</param>
                /// <returns></returns>
                public T Get<T>(string name, Expression<Func<ValidatorFormat, bool>> func)
                {
#if NETFRAMEWORK
                    return this._Value<T>(HttpContext.Current.Request.QueryString, name, func);
#else
                    return this._Value<T>(HttpContext.Current.Request.Query as IEnumerable<KeyValuePair<string, StringValues>>, name, func);
#endif
                }
#endregion

#region 获取表单值
                /// <summary>
                /// 获取表单值 原始值
                /// </summary>
                /// <param name="name">名称</param>
                /// <returns></returns>
                public string post(string name)
                {
                    return this._value(HttpContext.Current.Request.Form, name);
                }
                /// <summary>
                /// 获取表单值
                /// </summary>
                /// <param name="name">名称</param>
                /// <returns></returns>
                public string Post(string name)
                {
                    if (name.IsNullOrEmpty())
                    {
                        var list = new List<string>();
#if NETFRAMEWORK
                        HttpContext.Current.Request.Form.AllKeys.Each(a =>
                        {
                            if (HttpContext.Current.Request.Form[a] != null)
                                list.Add("{0}={1}".format(a, HttpContext.Current.Request.Form[a].ToString()));
                        });
#else
                        HttpContext.Current.Request.Form.Keys.Each(a =>
                        {
                            if (HttpContext.Current.Request.Form.TryGetValue(a, out var val))
                                list.Add("{0}={1}".format(a, val.ToString()));
                        });
#endif
                        return list.Join("&");
                    }
                    return this.Post<string>(name);
                }
                /// <summary>
                /// 获取表单值
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="defaultValue">默认值</param>
                /// <returns></returns>
                public T Post<T>(string name, T defaultValue = default(T))
                {
#if NETFRAMEWORK
                    return this._Value<T>(HttpContext.Current.Request.Form, name, defaultValue);
#else
                    return this._Value<T>(HttpContext.Current.Request.Form as IEnumerable<KeyValuePair<string, StringValues>>, name, defaultValue);
#endif
                }
                /// <summary>
                /// 获取表单值
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="format">验证规则</param>
                /// <returns></returns>
                public T Post<T>(string name, ValidateFormat format)
                {
#if NETFRAMEWORK
                    return this._Value<T>(HttpContext.Current.Request.Form, name, format);
#else
                    return this._Value<T>(HttpContext.Current.Request.Form as IEnumerable<KeyValuePair<string, StringValues>>, name, format);
#endif
                }
                /// <summary>
                /// 获取表单值 验证数据有效性 过滤SQL注入
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="func">Lambda表达式</param>
                /// <returns></returns>
                public T Post<T>(string name, Expression<Func<ValidatorFormat, bool>> func)
                {
#if NETFRAMEWORK
                    return this._Value<T>(HttpContext.Current.Request.Form, name, func);
#else
                    return this._Value<T>(HttpContext.Current.Request.Form as IEnumerable<KeyValuePair<string, StringValues>>, name, func);
#endif
                }
#endregion

#region 获取Header值
                /// <summary>
                /// 获取Header值 原始值
                /// </summary>
                /// <param name="name">名称</param>
                /// <returns></returns>
                public string header(string name)
                {
                    return this._value(HttpContext.Current.Request.Headers, name);
                }
                /// <summary>
                /// 获取表单值
                /// </summary>
                /// <param name="name">名称</param>
                /// <returns></returns>
                public string Header(string name)
                {
                    if (name.IsNullOrEmpty())
                    {
                        var list = new List<string>();
#if NETFRAMEWORK
                        HttpContext.Current.Request.Headers.AllKeys.Each(a =>
                        {
                            if (HttpContext.Current.Request.Headers[a] != null)
                                list.Add("{0}={1}".format(a, HttpContext.Current.Request.Headers[a].ToString()));
                        });
#else
                        HttpContext.Current.Request.Headers.Keys.Each(a =>
                        {
                            if (HttpContext.Current.Request.Headers.TryGetValue(a, out var val))
                                list.Add("{0}={1}".format(a, val.ToString()));
                        });
#endif
                        return list.Join("&");
                    }
                    return this.Header<string>(name);
                }
                /// <summary>
                /// 获取Header值
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="defaultValue">默认值</param>
                /// <returns></returns>
                public T Header<T>(string name, T defaultValue = default(T))
                {
#if NETFRAMEWORK
                    return this._Value<T>(HttpContext.Current.Request.Headers, name, defaultValue);
#else
                    return this._Value<T>(HttpContext.Current.Request.Headers as IEnumerable<KeyValuePair<string, StringValues>>, name, defaultValue);
#endif
                }
                /// <summary>
                /// 获取Header值
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="format">验证规则</param>
                /// <returns></returns>
                public T Header<T>(string name, ValidateFormat format)
                {
#if NETFRAMEWORK
                    return this._Value<T>(HttpContext.Current.Request.Headers, name, format);
#else
                    return this._Value<T>(HttpContext.Current.Request.Headers as IEnumerable<KeyValuePair<string, StringValues>>, name, format);
#endif
                }
                /// <summary>
                /// 获取Header值 验证数据有效性 过滤SQL注入
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <param name="func">Lambda表达式</param>
                /// <returns></returns>
                public T Header<T>(string name, Expression<Func<ValidatorFormat, bool>> func)
                {
#if NETFRAMEWORK
                    return this._Value<T>(HttpContext.Current.Request.Headers, name, func);
#else
                    return this._Value<T>(HttpContext.Current.Request.Headers as IEnumerable<KeyValuePair<string, StringValues>>, name, func);
#endif
                }
#endregion

#region 获取Cookie
                /// <summary>
                /// 获取Cookie
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="name">名称</param>
                /// <returns></returns>
                public T Cookie<T>(string name)
                {
                    if (name.IsNullOrEmpty()) return default(T);
#if NETFRAMEWORK
                    if (HttpContext.Current.Request.Cookies[name] != null)
                        return HttpContext.Current.Request.Cookies[name].Value.ReplaceSQL().ToCast<T>();
#else
                    if (HttpContext.Current.Request.Cookies.TryGetValue(name, out var val))
                        return val.ReplaceSQL().ToCast<T>();
#endif
                    return default(T);
                }
                /// <summary>
                /// 获取Cookie集
                /// </summary>
                /// <param name="name">名称</param>
                /// <returns></returns>
                public Dictionary<string, string> Cookies(string name)
                {
                    if (name.IsNullOrEmpty()) return new Dictionary<string, string>();
                    return XiaoFeng.Web.HttpCookie.Gets(name);
                }
        #endregion

        #region 获取值
#if NETFRAMEWORK
                /// <summary>
                /// 获取数据并验证有效性 过滤SQL注入
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="nameValue">数据集</param>
                /// <param name="name">名称</param>
                /// <param name="defaultValue">默认值</param>
                /// <returns></returns>
                private T _Value<T>(NameValueCollection nameValue, string name, T defaultValue = default(T))
                {
                    var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (nameValue.Count > 0)
                    {
                        nameValue.AllKeys.Each(a =>
                        {
                            data.Add(a, HttpContext.Current.Server.UrlDecode(nameValue[a]));
                        });
                    }
                    if (HttpContext.Current.Request.HttpMethod == "POST")
                    {
                        var stream = HttpContext.Current.Request.InputStream;
                        if (stream.Length > 0)
                        {
                            if (HttpContext.Current.Request.Files.Count == 0)
                            {
                                stream.Position = 0;
                                Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                using (var sr = new StreamReader(stream))
                                {
                                    var body = sr.ReadToEnd();
                                    if (body.IsJson())
                                    {
                                        d = body.JsonToObject<Dictionary<string, string>>();
                                        d.Each(a =>
                                        {
                                            if (!this.Data.ContainsKey(a.Key))
                                                this.Data.Add(a.Key, a.Value);
                                        });
                                    }
                                    else if (body.IsMatch(@"^([^=&]+=[^&]*&?)+$"))
                                    {
                                        body.GetMatches(@"(?<a>[^=&]+)=(?<b>[^&]*)").Each(a =>
                                        {
                                            var Key = a["a"];
                                            var Value = a["b"];
                                            if (!this.Data.ContainsKey(Key))
                                                this.Data.Add(Key, Value);
                                        });
                                    }
                                }
                                /*Dictionary<string, string> d = new StreamReader(stream).ReadToEnd().JsonToObject<Dictionary<string, string>>();
                                if (data == null) data = new Dictionary<string, string>();
                                d.Each(a =>
                                {
                                    var val = HttpContext.Current.Server.UrlDecode(a.Value);
                                    if (!data.Keys.Contains(a.Key))
                                        data.Add(a.Key, val);
                                    else
                                        data[a.Key] = val;
                                });*/
                            }
                        }
                    }
                    if (data == null || data.Count == 0) return defaultValue;
                    ValueTypes baseType = typeof(T).GetValueType();
                    if (name.IsNullOrEmpty())
                    {
                        if (baseType == ValueTypes.Struct || baseType == ValueTypes.Class)
                        {
                            T val = defaultValue;
                            object o = val;
                            typeof(T).GetMembers().Each(m =>
                            {
                                if (m.MemberType == MemberTypes.Property)
                                {
                                    PropertyInfo p = m as PropertyInfo;
                                    object _val = data[p.Name].GetValue(p.PropertyType);
                                    if (p.PropertyType == typeof(string))
                                    {
                                        if (_val.IsNotNullOrEmpty())
                                        {
                                            _val = _val.ToString().ReplaceSQL();
                                            p.SetValue(o, _val);
                                        }
                                    }
                                    else
                                        p.SetValue(val, _val);
                                }
                                else if (m.MemberType == MemberTypes.Field)
                                {
                                    FieldInfo f = m as FieldInfo;
                                    object _val = data[f.Name].GetValue(f.FieldType);
                                    if (f.FieldType == typeof(string))
                                    {
                                        if (_val.IsNotNullOrEmpty())
                                        {
                                            _val = _val.ToString().ReplaceSQL();
                                            f.SetValue(val, _val);
                                        }
                                    }
                                    else
                                        f.SetValue(o, _val);
                                }
                            });
                            val = (T)o;
                            return val;
                        }
                        else return defaultValue;
                    }
                    else
                        return data.ContainsKey(name) ? data[name].ReplaceSQL().ToCast(defaultValue) : defaultValue;
                }
                /// <summary>
                /// 获取数据并验证有效性 过滤SQL注入
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="nameValue">数据集</param>
                /// <param name="name">名称</param>
                /// <param name="format">验证规则</param>
                /// <returns></returns>
                private T _Value<T>(NameValueCollection nameValue, string name, ValidateFormat format)
                {
                    if (name.IsNotNullOrEmpty())
                        this.Result.AddRange(new ConditionValidator(name, nameValue[name], format).Result);
                    return this._Value<T>(nameValue, name);
                }
                /// <summary>
                /// 获取数据并验证有效性 过滤SQL注入
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="nameValue">数据集</param>
                /// <param name="name">名称</param>
                /// <param name="func">Lambda表达式</param>
                /// <returns></returns>
                private T _Value<T>(NameValueCollection nameValue, string name, Expression<Func<ValidatorFormat, bool>> func)
                {
                    string value = "";
                    if (!name.IsNullOrEmpty())
                    {
                        value = (nameValue == null || nameValue.Count == 0) ? "" : nameValue[name];
                        if (func != null)
                        {
                            string _value = func.Body.ToString().ReplacePattern(@".Equals\(", " == ");
                            List<Dictionary<string, string>> list = _value.GetMatches(@"\.(?<a>[a-z]+)(\s*==\s*(?<b>-?[\sa-z0-9\,\(""]+))?");
                            Dictionary<string, string> data = new Dictionary<string, string>();
                            list.Each(a =>
                            {
                                if (!data.ContainsKey(a["a"]))
                                {
                                    string _key = a["a"], _val = a["b"].Trim('"').RemovePattern(@"(new BetweenValue|Convert)\(");
                                    if (_key.IsMatch(@"^Is[a-z]+$"))
                                    {
                                        if (_key == "IsPattern")
                                            _val = _value.GetMatch(@"IsPattern == ""(?<a>.*?)""(\)[\s\)]?)");
                                        else
                                        {
                                            if (_value.IsMatch(@"(\s*|\()Not\([a-z]+\." + _key + @"+\)"))
                                                _val = "False";
                                            else if (_val.IsNullOrEmpty()) _val = "True";
                                        }
                                    }
                                    data.Add(_key, _val);
                                }
                            });
                            Type ValidatorType = Type.GetType("XiaoFeng.Validator.ConditionValidator");
                            Type FormatType = Type.GetType("XiaoFeng.Validator.ValidateFormat");
                            object format = Activator.CreateInstance(FormatType);
                            data.Each(d =>
                            {
                                PropertyInfo p = FormatType.GetProperty(d.Key, BindingFlags.Public | BindingFlags.IgnoreCase| BindingFlags.Instance);
                                string val = d.Value;
                                object o = null;
                                if (val.IsNullOrEmpty())
                                    o = null;
                                else if (d.Key == "Between")
                                {
                                    string[] _val = val.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    Type between = Type.GetType("XiaoFeng.Validator.BetweenValue");
                                    o = Activator.CreateInstance(between, new object[] { _val[0].Trim().ToDouble(), _val[1].Trim().ToDouble() });
                                }
                                else
                                {
                                    Type _t = p.PropertyType;
                                    if (_t.IsGenericType && _t.GetGenericTypeDefinition() == typeof(Nullable<>)) _t = _t.GetGenericArguments()[0];
                                    o = Convert.ChangeType(val, _t);
                                }
                                p.SetValue(format, o);
                            });
                            object Validator = Activator.CreateInstance(ValidatorType, new object[] { name, value, format });
                            PropertyInfo Result = ValidatorType.GetProperty("Result", BindingFlags.Public | BindingFlags.IgnoreCase| BindingFlags.Instance);
                            object result = Result.GetValue(Validator, null);
                            this.Result.AddRange(result as List<string>);
                        }
                    }
                    return this._Value<T>(nameValue, name);
                }
#else
        /// <summary>
        /// 获取数据并验证有效性 过滤SQL注入
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="nameValue">数据集</param>
        /// <param name="name">名称</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns></returns>
        private T _Value<T>(IEnumerable<KeyValuePair<string, StringValues>> nameValue, string name, T defaultValue = default(T))
                {
                    var Request = HttpContext.Current.Request;
                    Dictionary<string, StringValues> data = nameValue.ToDictionary(a => a.Key, a => a.Value);
                    if (Request.Method == "POST")
                    {
                        if (Request.HasFormContentType)
                        {
                            Request.Form.Keys.Each(a =>
                            {
                                data.Add(a, Request.Form[a].ToString().UrlDecode());
                            });
                        }
                        else
                        {
                            var stream = Request.Body;
                            if (stream.Length > 0 && stream.CanRead)
                            {
                                var ms = new MemoryStream();
                                stream.Position = 0;
                                stream.CopyTo(ms);
                                ms.Position = 0;
                                Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                using (var sr = new StreamReader(ms))
                                {
                                    var body = sr.ReadToEnd();
                                    if (body.IsMatch(@"^([^=&]+=[^&]*&?)+$"))
                                    {
                                        Dictionary<string, string> dic = new Dictionary<string, string>();
                                        body.GetMatches(@"(?<a>[^=&]+)=(?<b>[^&]*)").Each(a =>
                                        {
                                            var Key = a["a"];
                                            var Value = a["b"];
                                            if (!this.Data.ContainsKey(Key))
                                                this.Data.Add(Key, Value);
                                        });
                                    }
                                }
                            }
                        }
                    }
                    if (nameValue == null || nameValue.Count() == 0) return defaultValue;
                    ValueTypes baseType = typeof(T).GetValueType();
                    if (name.IsNullOrEmpty())
                    {
                        if (baseType == ValueTypes.Struct || baseType == ValueTypes.Class)
                        {
                            T val = defaultValue;
                            object o = val;
                            typeof(T).GetMembers().Each(m =>
                            {
                                if (m.MemberType == MemberTypes.Property)
                                {
                                    PropertyInfo p = m as PropertyInfo;
                                    object _val = data[p.Name].GetValue(p.PropertyType);
                                    if (p.PropertyType == typeof(string))
                                    {
                                        if (_val.IsNotNullOrEmpty())
                                        {
                                            _val = _val.ToString().ReplaceSQL();
                                            p.SetValue(o, _val);
                                        }
                                    }
                                    else
                                        p.SetValue(val, _val);
                                }
                                else if (m.MemberType == MemberTypes.Field)
                                {
                                    FieldInfo f = m as FieldInfo;
                                    object _val = data[f.Name].GetValue(f.FieldType);
                                    if (f.FieldType == typeof(string))
                                    {
                                        if (_val.IsNotNullOrEmpty())
                                        {
                                            _val = _val.ToString().ReplaceSQL();
                                            f.SetValue(val, _val);
                                        }
                                    }
                                    else
                                        f.SetValue(o, _val);
                                }
                            });
                            val = (T)o;
                            return val;
                        }
                        else return defaultValue;
                    }
                    else
                        return data.ContainsKey(name) ? data[name].ToString().ReplaceSQL().ToCast(defaultValue) : defaultValue;
                }
                /// <summary>
                /// 获取数据并验证有效性 过滤SQL注入
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="nameValue">数据集</param>
                /// <param name="name">名称</param>
                /// <param name="format">验证规则</param>
                /// <returns></returns>
                private T _Value<T>(IEnumerable<KeyValuePair<string, StringValues>> nameValue, string name, ValidateFormat format)
                {
                    string value = "";
                    var data = nameValue.ToDictionary(a => a.Key, a => a.Value);
                    if (!name.IsNullOrEmpty())
                    {
                        value = data[name];
                        this.Result.AddRange(new ConditionValidator(name, value, format).Result);
                    }
                    return this._Value<T>(nameValue, name);
                }
                /// <summary>
                /// 获取数据并验证有效性 过滤SQL注入
                /// </summary>
                /// <typeparam name="T">类型</typeparam>
                /// <param name="nameValue">数据集</param>
                /// <param name="name">名称</param>
                /// <param name="func">Lambda表达式</param>
                /// <returns></returns>
                private T _Value<T>(IEnumerable<KeyValuePair<string, StringValues>> nameValue, string name, Expression<Func<ValidatorFormat, bool>> func)
                {
                    string value = "";
                    var _data = nameValue.ToDictionary(a => a.Key, a => a.Value);
                    if (!name.IsNullOrEmpty())
                    {
                        value = (nameValue == null || nameValue.Count() == 0) ? "" : _data[name].ToString();
                        if (func != null)
                        {
                            string _value = func.Body.ToString().ReplacePattern(@".Equals\(", " == ");
                            List<Dictionary<string, string>> list = _value.GetMatches(@"\.(?<a>[a-z]+)(\s*==\s*(?<b>-?[\sa-z0-9\,\(""]+))?");
                            Dictionary<string, string> data = new Dictionary<string, string>();
                            list.Each(a =>
                            {
                                if (!data.ContainsKey(a["a"]))
                                {
                                    string _key = a["a"], _val = a["b"].Trim('"').RemovePattern(@"(new BetweenValue|Convert)\(");
                                    if (_key.IsMatch(@"^Is[a-z]+$"))
                                    {
                                        if (_key == "IsPattern")
                                            _val = _value.GetMatch(@"IsPattern == ""(?<a>.*?)""(\)[\s\)]?)");
                                        else
                                        {
                                            if (_value.IsMatch(@"(\s*|\()Not\([a-z]+\." + _key + @"+\)"))
                                                _val = "False";
                                            else if (_val.IsNullOrEmpty()) _val = "True";
                                        }
                                    }
                                    data.Add(_key, _val);
                                }
                            });
                            Type ValidatorType = Type.GetType("XiaoFeng.Validator.ConditionValidator");
                            Type FormatType = Type.GetType("XiaoFeng.Validator.ValidateFormat");
                            object format = Activator.CreateInstance(FormatType);
                            data.Each(d =>
                            {
                                PropertyInfo p = FormatType.GetProperty(d.Key,BindingFlags.Public| BindingFlags.IgnoreCase | BindingFlags.Instance);
                                string val = d.Value;
                                object o = null;
                                if (val.IsNullOrEmpty())
                                    o = null;
                                else if (d.Key == "Between")
                                {
                                    string[] _val = val.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    Type between = Type.GetType("XiaoFeng.Validator.BetweenValue");
                                    o = Activator.CreateInstance(between, new object[] { _val[0].Trim().ToDouble(), _val[1].Trim().ToDouble() });
                                }
                                else
                                {
                                    Type _t = p.PropertyType;
                                    if (_t.IsGenericType && _t.GetGenericTypeDefinition() == typeof(Nullable<>)) _t = _t.GetGenericArguments()[0];
                                    o = Convert.ChangeType(val, _t);
                                }
                                p.SetValue(format, o);
                            });
                            object Validator = Activator.CreateInstance(ValidatorType, new object[] { name, value, format });
                            PropertyInfo Result = ValidatorType.GetProperty("Result",BindingFlags.Public| BindingFlags.IgnoreCase | BindingFlags.Instance);
                            object result = Result.GetValue(Validator, null);
                            this.Result.AddRange(result as List<string>);
                        }
                    }
                    return this._Value<T>(nameValue, name);
                }
#endif
        /// <summary>
        /// 获取原始值
        /// </summary>
        /// <param name="nameValue">数据集</param>
        /// <param name="name">名称</param>
        /// <returns></returns>
#if NETFRAMEWORK
        private string _value(NameValueCollection nameValue, string name)
                {
                    if (name.IsNullOrEmpty() || nameValue == null || nameValue.Count == 0) return "";
                    return nameValue[name];
                }
#else
                private string _value(IEnumerable<KeyValuePair<string,StringValues>> nameValue, string name)
                {
                    if (name.IsNullOrEmpty() || nameValue == null || nameValue.Count() == 0) return "";
                    var data = nameValue.ToDictionary(a => a.Key, a => a.Value);
                    return data.TryGetValue(name, out var val) ? val.ToString() : "";
                }
#endif
#endregion
    }
}