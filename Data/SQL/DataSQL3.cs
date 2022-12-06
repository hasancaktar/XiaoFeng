﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
*  Create Time : 2017-12-18 10:18:41                            *
*  Version : v 1.0.0                                            *
*  CLR Version : 4.0.30319.42000                                *
*****************************************************************/
namespace XiaoFeng.Data.SQL
{
    #region 多表存储结构
    /// <summary>
    /// 多表存储结构
    /// </summary>
    public class DataSQL3 : DataSQL2
    {
        #region 构造器
        /// <summary>
        /// 无参构造器
        /// </summary>
        public DataSQL3()
        {
            this.Columns = new List<string>();
            this.Counts = 0;
            this.SQLType = SQLType.select;
            this.GroupByString = new List<string>();
            this.JoinType = new Dictionary<string, JoinType>();
            this.ModelType = new Dictionary<TableType, Type>();
            this.OnString = new Dictionary<string, string>();
            this.TableName = new Dictionary<TableType, string>();
            this.UpdateColumns = new List<object>();
            this.WhereString = new Dictionary<TableType, string>();
        }
        #endregion

        #region 属性
        /// <summary>
        /// ON条件
        /// </summary>
        public new Dictionary<string, string> OnString { get; set; }
        /// <summary>
        /// 联表类型
        /// </summary>
        public new Dictionary<string, JoinType> JoinType { get; set; }
        #endregion

        #region 方法

        #region 设置On JoinType
        /// <summary>
        /// 设置 On和JoinType
        /// </summary>
        /// <param name="OnString">On字符串</param>
        /// <param name="joinType">关联表类型</param>
        public void SetOnAndJoinType(string OnString, JoinType joinType = SQL.JoinType.Left)
        {
            if (OnString.IsNullOrEmpty()) return;
            OnString.RemovePattern(@"(^\s*AND\s*|\s*AND\s*$)").SplitPattern(@"\s*AND\s*").Each(a =>
            {
                Dictionary<string, string> d = a.ReplacePattern(@"Convert\(nvarchar\(20\),([^\)]+)\)", "$1").GetMatchs(@"\s*(?<a>[a-z]+)\.[\[`][^\.`\[\]]+[\]`]\s*=\s*(?<b>[a-z]+)\.[\[`][^\.`\[\]]+[\]`]\s*");
                if (!d.ContainsKey("a") || !d.ContainsKey("b")) return true;
                string _Keys = d["a"] + d["b"];
                this.SetJoinType(_Keys, joinType);
                this.SetOn(_Keys, a);
                return true;
            });
        }
        #endregion

        #region JoinType
        /// <summary>
        /// 设置JoinType
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="jType">类型</param>
        public virtual void SetJoinType(string name, JoinType jType)
        {
            if (name.IsNullOrEmpty()) return;
            if (this.JoinType == null) this.JoinType = new Dictionary<string, JoinType>();
            name = name.OrderBy();
            if (this.JoinType.ContainsKey(name)) this.JoinType[name] = jType;
            else this.JoinType.Add(name, jType);
        }
        /// <summary>
        /// 获取JoinType
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public virtual JoinType GetJoinType(string name)
        {
            if (name.IsNullOrEmpty() || this.JoinType == null) return SQL.JoinType.Left;
            return this.JoinType[name];
        }
        #endregion

        #region On字符串
        /// <summary>
        /// 设置On字符串
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="onString">On字符串</param>
        public virtual void SetOn(string name, string onString)
        {
            if (name.IsNullOrEmpty() || onString.IsNullOrEmpty()) return;
            if (this.OnString == null) this.OnString = new Dictionary<string, string>();
            name = name.OrderBy();
            if (this.OnString.ContainsKey(name))
                this.OnString[name] = this.OnString[name] + " AND {0}".format(onString);
            else
            {
                var firstChar = name[0].ToString();
                var LastChar = name[1].ToString();
                if (this.OnString.Where(a => a.Key.IsMatch(firstChar)).Any() && this.OnString.Where(a => a.Key.IsMatch(LastChar)).Any())
                {
                    var kValue = this.OnString.Where(a => a.Key.IsMatch(LastChar + "$")).Last();
                    this.OnString[kValue.Key] = this.OnString[kValue.Key] + " AND {0}".format(onString);
                }
                else
                    this.OnString.Add(name, onString);
            }
        }
        /// <summary>
        /// 获取On字符串
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public virtual string GetOn(string name)
        {
            if (name.IsNullOrEmpty() || this.OnString == null) return null;
            this.OnString.TryGetValue(name, out string value);
            if (value.IsNullOrEmpty()) return value;
            return value.IsMatch(@"\s*on\s+") ? value : " ON {0}".format(value);
        }
        #endregion

        #region 获取SQL
        /// <summary>
        /// 获取SQL表
        /// </summary>
        /// <returns></returns>
        public override string GetSQLTable()
        {
            return this.GetSQLString("select {Columns} from {JoinTable} {WhereString} {GroupBy} {OrderBy}");
        }
        /// <summary>
        /// 获取SQL
        /// </summary>
        /// <param name="SQLString">SQL模板</param>
        /// <returns></returns>
        public override string GetSQLString(string SQLString = "")
        {
            Stopwatch sTime = new Stopwatch();
            sTime.Start();
            string SQLTemplate = "";
            if ((DbProviderType.SQLite | DbProviderType.MySql | DbProviderType.Oracle | DbProviderType.Dameng).HasFlag(this.Config.ProviderType))
            {
                SQLTemplate = @"select {Column} from {JoinTable} {WhereString} {GroupBy} {OrderBy} limit {Limit},{Top}";
            }
            else
            {
                SQLTemplate = @"select {Top} {Column} from (
select {Limits} row_number() over({OrderBy}) as TempID, * from 
(
    select {Columns} from {JoinTable} {WhereString} {GroupBy}
) as YYYY
) as ZZZZ where TempID > {Limit};";
            }
            Dictionary<string, string> data = new Dictionary<string, string>();
            if (!SQLString.IsNullOrEmpty()) SQLTemplate = SQLString;
            if (this.TableName == null) this.TableName = new Dictionary<TableType, string>();

            string Columns = this.GetColumns(), OrderBy = this.GetOrderBy();
            data.Add("Columns", Columns);
            string Column = Columns.ReplacePattern(@"[a-z]+\.\[[a-z0-9_-]+\]\s+as\s+([a-z-0-9_-]+)\s*", "$1");
            /*Top*/
            data.Add("Top", this.GetTop());
            /*Limit*/
            data.Add("Limit", this.GetLimit());
            /*Limits*/
            data.Add("Limits", this.GetLimits());
            /*Column*/
            data.Add("Column", Column);
            /*处理结果集Where*/
            string WhereStrings = "";
            /*判断表是否有条件*/
            if (this.WhereString != null && this.WhereString.Count > 0)
                this.WhereString.Each(w =>
                {
                    if (w.Key == TableType.TResult)
                    {
                        string _Where = this.GetValue(this.WhereString, TableType.TResult);
                        if (WhereStrings.IsNullOrEmpty())
                            WhereStrings = " where ";
                        WhereStrings += (WhereStrings.IsNullOrEmpty() ? " where " : " and ") + _Where;

                        WhereStrings += (WhereStrings.IsNullOrEmpty() ? " where " : " and ") + w.Value;
                        return true;
                    }
                    if (!this.TableName.ContainsKey(w.Key))
                    {
                        if (!this.ModelType.ContainsKey(w.Key)) return false;
                        TableAttribute Table = this.ModelType[w.Key].GetTableAttribute();
                        this.TableName.Add(w.Key,
                            Table == null ?
                            this.ModelType[w.Key].Name :
                            Table.Name ?? this.ModelType[w.Key].Name);
                    }
                    string TableName = this.TableName[w.Key];
                    if (TableName.IsMatch(@"^\s*select\s+"))
                    {
                        this.TableName[w.Key] += (TableName.IsMatch(@"\s+where\s+") ? " and " : " where ") + w.Value;
                    }
                    if (this.TableName[w.Key].IndexOf("select") > -1)
                        this.TableName[w.Key] = "select * from {0} where {1}".format(FieldFormat(TableName), w.Value);
                    else
                    {
                        /*优化SQL语句，把子条件拿到 on 后执行*/
                        WhereStrings += (WhereStrings.IsNullOrEmpty() ? " where " : " and ") + w.Value.Replace("[", this.Prefix[w.Key] + ".[");
                    }
                    return true;
                });
            data.Add("WhereString", WhereStrings);
            /*拼接关联表*/
            string SubSQL = "";
            if (this.JoinType == null || this.JoinType.Count == 0) return "";
            this.JoinType.Each((KeyValuePair<string, JoinType> j) =>
            {
                string Key = j.Key;
                if (Key.Length == 2)
                {
                    string onString = this.GetOn(Key);
                    if (!onString.IsNullOrEmpty())
                    {
                        TableType TableTypeA = this.Prefix.FindByValue(Key[0].ToString()).Key,
                                  TableTypeB = this.Prefix.FindByValue(Key[1].ToString()).Key;
                        if (this.TableName == null || !this.TableName.ContainsKey(TableTypeA) || !this.TableName.ContainsKey(TableTypeB))
                        {
                            if (!this.TableName.ContainsKey(TableTypeA))
                            {
                                if (!this.ModelType.ContainsKey(TableTypeA)) { SubSQL = ""; return false; }
                                TableAttribute Table = this.ModelType[TableTypeA].GetTableAttribute();
                                this.TableName.Add(TableTypeA,
                                    Table == null ?
                                    this.ModelType[TableTypeA].Name :
                                    Table.Name ?? this.ModelType[TableTypeA].Name);
                            }
                            if (!this.TableName.ContainsKey(TableTypeB))
                            {
                                if (!this.ModelType.ContainsKey(TableTypeB)) { SubSQL = ""; return false; }
                                TableAttribute Table = this.ModelType[TableTypeB].GetTableAttribute();
                                this.TableName.Add(TableTypeB,
                                    Table == null ?
                                    this.ModelType[TableTypeB].Name :
                                    Table.Name ?? this.ModelType[TableTypeB].Name);
                            }
                        }
                        string TableA = this.TableName[TableTypeA],
                        TableB = this.TableName[TableTypeB];
                        if (j.Value == Data.SQL.JoinType.Union)
                        {
                            TableA = (TableA.IsMatch(@"select ") ? "({0}) as {1}" : "{0}").format(TableA, Key[0]);
                            TableB = (TableB.IsMatch(@"select ") ? "({0})" : "select * from {0}").format(TableB, Key[1]);
                        }
                        else
                        {
                            TableA = ((TableA.IsMatch(@"select ") ? "({0})" : "{0}") + " as {1}").format(TableA, Key[0]);
                            TableB = ((TableB.IsMatch(@"select ") ? "({0})" : "{0}") + " as {1}").format(TableB, Key[1]);
                        }
                        if (SubSQL.IsNullOrEmpty()) SubSQL = TableA;
                        SubSQL += " {0} {1} {2} ".format(this.JoinTypes[j.Value], TableB, onString);
                    }
                }
                return true;
            });
            if (SubSQL.IsNullOrEmpty()) return "";
            /*子表*/
            data.Add("JoinTable", SubSQL);
            /*GroupBy*/
            data.Add("GroupBy", this.GetGroupBy());
            /*OrderBy*/
            if (SQLTemplate.IsMatch(@"row_number\(\)"))
                OrderBy = OrderBy.IsNullOrEmpty() ? "order by {0} asc".format(Column.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)[0]) : OrderBy;
            if (this.Top == -1)
            {
                OrderBy = OrderBy.ReplacePattern(@"(\s+)asc([,\s]?)", "$1{asc}$2").ReplacePattern(@"(\s+)desc([,\s]?)", "$1asc$2").ReplacePattern(@"(\s+)\{asc\}([,\s]?)", "$1desc$2");
                this.OrderByString = OrderBy;
                this.Top = 1;
            }
            data.Add("OrderBy", OrderBy);
            string SQL = this.SQLString = SQLTemplate.format(data).ReplacePattern(@"limit 0,[0\s]*;?\s*$", ";").ReplacePattern(@"\s*(;?)\s*$", "$1");
            if (this.Config.ProviderType == DbProviderType.OleDb)
            {
                SQL = this.SQLString = SQL.ReplacePattern(@"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\.\d+", "$1");
            }
            sTime.Stop();
            this.SpliceSQLTime += sTime.ElapsedMilliseconds;
            this.CreateCacheKey();
            //if (this.Config.ProviderType != DbProviderType.SqlServer) SQL = SQL.RemovePattern(@"[\[\]]");
            return SQL;
        }
        #endregion

        #region 复制数据
        /// <summary>
        /// 复制数据
        /// </summary>
        /// <param name="dSQL">DataSQL3对象</param>
        public void Done(DataSQL3 dSQL)
        {
            if (dSQL == null) return;
            DataSQL3 SQL = dSQL.ToJson().JsonToObject<DataSQL3>();
            PropertyInfo[] ps = this.GetType().GetProperties();
            PropertyInfo[] pd = SQL.GetType().GetProperties();
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].CanWrite && ps[i].CanRead)
                {
                    ps[i].SetValue(this, pd[i].GetValue(SQL, null), null);
                }
            }
        }
        /// <summary>
        /// 复制当前对象
        /// </summary>
        /// <returns></returns>
        public new virtual object Clone()
        {
            return this.MemberwiseClone();
        }
        #endregion

        #endregion
    }
    #endregion
}
