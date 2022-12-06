﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
/****************************************************************
*  Copyright © (2017) www.fayelf.com All Rights Reserved.       *
*  Author : jacky                                               *
*  QQ : 7092734                                                 *
*  Email : jacky@fayelf.com                                     *
*  Site : www.fayelf.com                                        *
*  Create Time : 2017-10-25 11:59:42                            *
*  Version : v 1.0.0                                            *
*  CLR Version : 4.0.30319.42000                                *
*****************************************************************/
namespace XiaoFeng.Json
{
    /// <summary>
    /// 写Json
    /// </summary>
    public class JsonWriterX : Disposable
    {
        #region 构造器
        /// <summary>
        /// 无参构造器
        /// </summary>
        public JsonWriterX() { this.SerializerSetting = JsonParser.DefaultSettings ?? new JsonSerializerSetting(); }
        /// <summary>
        /// 设置日期格式Json格式设置        
        /// </summary>
        /// <param name="formatterSetting">Json格式设置</param>
        public JsonWriterX(JsonSerializerSetting formatterSetting)
        {
            this.SerializerSetting = formatterSetting;
        }
        #endregion

        #region 属性
        ///<summary>
        /// 数据
        /// </summary>
        public StringBuilder Builder = new StringBuilder();
        /// <summary>
        /// 深度
        /// </summary>
        private int _Depth = 0;
        /// <summary>
        /// 字典集
        /// </summary>
        private Dictionary<object, int> _DepthDict = new Dictionary<object, int>();
        /// <summary>
        /// Json格式
        /// </summary>
        public JsonSerializerSetting SerializerSetting { get; set; }
        #endregion

        #region 方法

        #region 写数据
        /// <summary>
        /// 写数据
        /// </summary>
        /// <param name="value">对象</param>
        /// <returns></returns>
        public void WriteValue(object value)
        {
            /*switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Empty:
                    Builder.Append("null");break;
                case TypeCode.String:
                case TypeCode.Char:
                    WriteString(value + "");break;
                case TypeCode.Boolean:
                    Builder.Append((value + "").ToLower());break;
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.Single:
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    Builder.Append(value.ToString());break;
                case TypeCode.DateTime:
                    WriteDateTime((DateTime)value);break;
                default: WriteArray((IEnumerable)value);break;
            }*/

            if (value is string || value is char)
                WriteString(value + "");
            else if (value == null)
                Builder.Append("null");
            else if (value is bool)
                Builder.Append((value + "").ToLower());
            else if (
                value is int || value is long || value is double ||
                value is decimal || value is Single || value is float ||
                value is byte || value is short ||
                value is sbyte || value is ushort ||
                value is uint || value is ulong
            )
                Builder.Append(((IConvertible)value).ToString(NumberFormatInfo.InvariantInfo));
            else if (value is DateTime dateTime)
                WriteDateTime(dateTime);
            else if (value is Guid guid)
                WriteGuid(guid);
            else if (value is IDictionary<string, object> dic)
                WriteStringDictionary(dic);
            else if (value is IDictionary dictionary && value.GetType().IsGenericType && value.GetType().GetGenericArguments()[0] == typeof(string))
                WriteStringDictionary(dictionary);
            else if (value is System.Dynamic.ExpandoObject)
                WriteStringDictionary((IDictionary<string, object>)value);
            else if (value is IDictionary dict)
                WriteDictionary(dict);
            else if (value is byte[] buf)
            {
                WriteStringFast(Convert.ToBase64String(buf, 0, buf.Length, Base64FormattingOptions.None));
            }
            else if (value is StringDictionary sdict)
                WriteSD(sdict);
            else if (value is NameValueCollection nvc)
                WriteNV(nvc);
            else if (value is IEnumerable ea)
                WriteArray(ea);
            else if (value is Enum)
            {
                if (SerializerSetting.EnumValueType == EnumValueType.Name)
                    value = value.ToString();
                else if (SerializerSetting.EnumValueType == EnumValueType.Description)
                    value = value.GetType().GetField(value.ToString()).GetDescription();
                else value = value.GetValue(Enum.GetUnderlyingType(value.GetType()) ?? typeof(Int32));
                WriteValue(value);
            }
            else if (value is DataTable dt)
                WriteDataTable(dt);
            else if (value is DataRow dr)
                WriteDataRow(dr);
            else if (value is Type t)
                WriteType(t);
            else if (value is Delegate)
                WriteString(value.ToString());
            else if (value is JsonValue jsonValue)
                WriteJsonValue(jsonValue);
            else
                WriteObject(value);
        }
        #endregion

        #region 写时间
        /// <summary>
        /// 写时间
        /// </summary>
        /// <param name="dateTime">时间</param>
        private void WriteDateTime(DateTime dateTime)
        {
            Builder.AppendFormat("\"{0}\"", dateTime.ToString(this.SerializerSetting.DateTimeFormat));
        }
        #endregion

        #region 写Guid
        /// <summary>
        /// 写Guid
        /// </summary>
        /// <param name="guid">guid</param>
        private void WriteGuid(Guid guid)
        {
            Builder.AppendFormat("\"{0}\"", guid.ToString(SerializerSetting.GuidFormat));
        }
        #endregion

        #region 写对象
        /// <summary>
        /// 写对象
        /// </summary>
        /// <param name="obj">对象</param>
        private void WriteObject(object obj)
        {
            if (!_DepthDict.TryGetValue(obj, out var i)) _DepthDict.Add(obj, _DepthDict.Count + 1);
            Builder.Append('{');
            _Depth++;
            if (_Depth > SerializerSetting.MaxDepth) throw new JsonException("超过了序列化最大深度 " + SerializerSetting.MaxDepth);
            var t = obj.GetType();
            var first = true;
            t.GetPropertiesAndFields().Each(m =>
            {
                if (m is FieldInfo || m is PropertyInfo)
                {
                    JsonIgnoreAttribute ignore = m.GetCustomAttribute<JsonIgnoreAttribute>();
                    if (ignore != null) return;
                    string name = "";
                    object value = null;
                    Type mType = m.DeclaringType;
                    if (m is FieldInfo f)
                    {
                        name = f.Name;
                        mType = f.FieldType;
                        value = f.GetValue(obj);
                    }
                    else if (m is PropertyInfo p)
                    {
                        if (p.IsIndexer()) return;
                        name = p.Name;
                        mType = p.PropertyType;
                        value = p.GetValue(obj, null);
                    }
                    if (this.SerializerSetting.IsComment)
                    {
                        var desc = m.GetDescription();
                        if (desc.IsNotNullOrEmpty()) name = "/*{0}*/{1}".format(desc, name);
                    }
                    JsonConverterAttribute jsonConverter = m.GetCustomAttribute<JsonConverterAttribute>();
                    if (jsonConverter != null)
                    {
                        if (jsonConverter.ConverterType == typeof(StringEnumConverter) && mType.IsEnum)
                            value = value.ToString();
                        else if (jsonConverter.ConverterType == typeof(DescriptionConverter))
                        {
                            if (mType.IsEnum)
                            {
                                value = value.GetType().GetField(value.ToString()).GetDescription();
                            }
                            else
                                value = m.GetDescription();
                        }
                    }
                    if (!first) Builder.Append(',');
                    first = false;
                    WritePair(name, value);
                }
            });
            Builder.Append('}');
            _Depth--;
        }
        #endregion

        #region 写DataTable
        /// <summary>
        /// 写DataTable
        /// </summary>
        /// <param name="data">数据表</param>
        private void WriteDataTable(DataTable data)
        {
            Builder.Append('[');
            var first = true;
            data.Rows.Each<DataRow>(dr =>
            {
                if (!first) Builder.Append(',');
                Builder.Append("{");
                first = false;
                var _first = true;
                data.Columns.Each<DataColumn>(c =>
                {
                    if (!_first) Builder.Append(',');
                    _first = false;
                    Builder.Append("\"" + c.ColumnName + "\":");
                    WriteValue(dr[c.ColumnName]);
                });
                Builder.Append("}");
            });
            Builder.Append(']');
        }
        #endregion

        #region 写DataRow
        /// <summary>
        /// 写DataRow
        /// </summary>
        /// <param name="dr">数据</param>
        private void WriteDataRow(DataRow dr)
        {
            var first = true;
            dr.Table.Columns.Each<DataColumn>(c =>
            {
                if (!first) Builder.Append(",");
                first = false;
                Builder.Append("\"" + c.ColumnName + "\":");
                WriteValue(dr[c.ColumnName]);
            });
        }
        #endregion

        #region 写键值对
        /// <summary>
        /// 写键值对
        /// </summary>
        /// <param name="nvs">键值对</param>
        private void WriteNV(NameValueCollection nvs)
        {
            Builder.Append('{');
            var first = true;
            foreach (string item in nvs)
            {
                if (nvs[item] != null)
                {
                    if (!first) Builder.Append(',');
                    first = false;
                    var name = item;
                    WritePair(name, nvs[item]);
                }
            }
            Builder.Append('}');
        }
        /// <summary>
        /// 写键值对
        /// </summary>
        /// <param name="name">键</param>
        /// <param name="value">值</param>
        private void WritePair(String name, Object value)
        {
            WriteStringFast(name);
            Builder.Append(':');
            WriteValue(value);
        }
        #endregion

        #region 写数组
        /// <summary>
        /// 写数组
        /// </summary>
        /// <param name="arr">数组</param>
        private void WriteArray(IEnumerable arr)
        {
            Builder.Append('[');
            var first = true;
            foreach (var obj in arr)
            {
                if (!first) Builder.Append(',');
                first = false;
                //Builder.Append(obj);
                this.WriteValue(obj);
            }
            Builder.Append(']');
        }
        #endregion

        #region 写字典
        /// <summary>
        /// 写字典
        /// </summary>
        /// <param name="dic">字典</param>
        private void WriteSD(StringDictionary dic)
        {
            Builder.Append('{');
            var first = true;
            foreach (DictionaryEntry item in dic)
            {
                if (item.Value != null)
                {
                    if (!first) Builder.Append(',');
                    first = false;
                    var name = (String)item.Key;
                    WritePair(name, item.Value);
                }
            }
            Builder.Append('}');
        }
        /// <summary>
        /// 写字典
        /// </summary>
        /// <param name="dic">字典</param>
        private void WriteStringDictionary(IDictionary dic)
        {
            Builder.Append('{');
            var first = true;
            foreach (DictionaryEntry item in dic)
            {
                if (item.Value != null)
                {
                    if (!first) Builder.Append(',');
                    first = false;
                    var name = (String)item.Key;
                    WritePair(name, item.Value);
                }
            }
            Builder.Append('}');
        }
        /// <summary>
        /// 写字典
        /// </summary>
        /// <param name="dic">字典</param>
        private void WriteStringDictionary(IDictionary<string, object> dic)
        {
            Builder.Append('{');
            var first = true;
            foreach (var item in dic)
            {
                if (item.Value != null)
                {
                    if (!first) Builder.Append(',');
                    first = false;
                    var name = item.Key;
                    WritePair(name, item.Value);
                }
            }
            Builder.Append('}');
        }
        /// <summary>
        /// 写字典
        /// </summary>
        /// <param name="dic">字典</param>
        private void WriteDictionary(IDictionary dic)
        {
            Builder.Append("{");
            var first = true;
            foreach (DictionaryEntry a in dic)
            {
                if (!first) Builder.Append(',');
                else first = false;
                WritePair(a.Key.ToString(), a.Value);
            };
            Builder.Append("}");
        }
        #endregion

        #region 写字符串
        /// <summary>
        /// 写字符串
        /// </summary>
        /// <param name="str">字符串</param>
        private void WriteStringFast(string str)
        {
            var comment = "";
            if (str.IsMatch(@"^\/\*[\s\S]*?\*\/"))
            {
                var _ = str.GetMatchs(@"^(?<a>\/\*[\s\S]*?\*\/)(?<b>[\s\S]*)$");
                str = _["b"];
                comment = _["a"];
            }
            Builder.Append('\"');
            if (this.SerializerSetting.IgnoreCase) str = str.ToLower();
            Builder.Append(str);
            Builder.Append('\"');
            if (comment.IsNotNullOrEmpty())
                Builder.Append(" " + comment);
        }
        /// <summary>
        /// 写字符串
        /// </summary>
        /// <param name="str">字符串</param>
        private void WriteString(string str)
        {
            Builder.Append('\"');
            var idx = -1;
            var len = str.Length;
            for (var index = 0; index < len; ++index)
            {
                var c = str[index];
                if (c != '\t' && c != '\n' && c != '\r' && c != '\"' && c != '\\')// && c != ':' && c!=',')
                {
                    if (idx == -1) idx = index;
                    continue;
                }
                if (idx != -1)
                {
                    Builder.Append(str, idx, index - idx);
                    idx = -1;
                }
                switch (c)
                {
                    case '\t': Builder.Append("\\t"); break;
                    case '\r': Builder.Append("\\r"); break;
                    case '\n': Builder.Append("\\n"); break;
                    case '"':
                    case '\\': Builder.Append('\\'); Builder.Append(c); break;
                    default:
                        Builder.Append(c);

                        break;
                }
            }
            if (idx != -1) Builder.Append(str, idx, str.Length - idx);
            Builder.Append('\"');
        }
        #endregion

        #region 写类型
        /// <summary>
        /// 写类型
        /// </summary>
        /// <param name="type">类型</param>
        private void WriteType(Type type)
        {
            Builder.Append('\"');
            Builder.Append(type.AssemblyQualifiedName);
            Builder.Append('\"');
        }
        #endregion

        #region 写JsonValue
        /// <summary>
        /// 写JsonValue
        /// </summary>
        /// <param name="jsonValue"></param>
        private void WriteJsonValue(JsonValue jsonValue)
        {
            switch (jsonValue.Type)
            {
                case JsonType.Array:
                    WriteValue(jsonValue.AsArray()); break;
                case JsonType.Object:
                    WriteValue(jsonValue.AsObject()); break;
                case JsonType.Bool:
                case JsonType.Byte:
                case JsonType.DateTime:
                case JsonType.Float:
                case JsonType.Char:
                case JsonType.Guid:
                case JsonType.Null:
                case JsonType.Number:
                case JsonType.String:
                case JsonType.Type:
                    WriteValue(jsonValue.value); break;
            }
        }
        #endregion

        #region 格式化Json文本
        /// <summary>格式化Json文本</summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public string Format(string json)
        {
            var sb = new StringBuilder();
            var escaping = false;
            var inQuotes = false;
            var indentation = 0;
            foreach (var ch in json)
            {
                if (escaping)
                {
                    escaping = false;
                    sb.Append(ch);
                }
                else
                {
                    if (ch == '\\')
                    {
                        escaping = true;
                        sb.Append(ch);
                    }
                    else if (ch == '\"')
                    {
                        inQuotes = !inQuotes;
                        sb.Append(ch);
                    }
                    else if (!inQuotes)
                    {
                        if (ch == ',')
                        {
                            sb.Append(ch);
                            sb.Append("\r\n");
                            sb.Append(' ', indentation * 2);
                        }
                        else if (ch == '[' || ch == '{')
                        {
                            sb.Append(ch);
                            sb.Append("\r\n");
                            sb.Append(' ', ++indentation * 2);
                        }
                        else if (ch == ']' || ch == '}')
                        {
                            sb.Append("\r\n");
                            sb.Append(' ', --indentation * 2);
                            sb.Append(ch);
                        }
                        else if (ch == ':')
                        {
                            sb.Append(ch);
                            sb.Append(' ', 1);
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                }
            }
            return sb.ToString().ReplacePattern(@"/\*[\s\S]*?\*/", m =>
            {
                return m.Groups[0].Value.RemovePattern(@"([\r\n]+|\s{2,})");
            });
        }
        #endregion

        #endregion
    }
}