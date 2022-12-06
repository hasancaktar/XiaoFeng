﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
/****************************************************************
*  Copyright © (2017) www.fayelf.com All Rights Reserved.       *
*  Author : jacky                                               *
*  QQ : 7092734                                                 *
*  Email : jacky@fayelf.com                                     *
*  Site : www.fayelf.com                                        *
*  Create Time : 2017-06-29 09:11:53                            *
*  Version : v 1.0.0                                            *
*  CLR Version : 4.0.30319.42000                                *
*****************************************************************/
namespace XiaoFeng.Data
{
    /// <summary>
    /// MySql 数据库操作类
    /// Verstion : 1.0.0
    /// Create Time : 2018/06/29 09:11:53
    /// Update Time : 2018/06/29 09:11:53
    /// </summary>
    public class MySqlHelper:DataHelper,IDbHelper
    {
        #region 构造器
        /// <summary>
        /// 无参构造器
        /// </summary>
        public MySqlHelper() { this.ProviderType = DbProviderType.MySql; }
        /// <summary>
        /// 设置数据库连接字符串
        /// </summary>
        /// <param name="ConnectionString">数据库连接字符串</param>
        public MySqlHelper(string ConnectionString) : this() { this.ConnectionString = ConnectionString; }
        /// <summary>
        /// 设置数据库连接
        /// </summary>
        /// <param name="connectionConfig">数据库连接配置</param>
        public MySqlHelper(ConnectionConfig connectionConfig) : base(connectionConfig)
        {
            this.ProviderType = DbProviderType.MySql;
        }
        #endregion

        #region 属性

        #endregion

        #region 方法

        #region 获取当前数据库所有用户表
        /// <summary>
        /// 获取当前数据库所有用户表
        /// </summary>
        /// <returns></returns>
        public virtual List<string> GetTables()
        {
            return this.ExecuteDataTable(@"select table_name from information_schema.tables where table_schema=database() and (table_type='base table' or table_type='BASE TABLE') order by table_name asc;").ToList<string>();
        }
        #endregion

        #region 获取当前数据库所有用户视图
        /// <summary>
        /// 获取当前数据库所有用户视图
        /// </summary>
        /// <returns></returns>
        public virtual List<ViewAttribute> GetViews()
        {
            return this.QueryList<ViewAttribute>(@"select table_name as Name,view_Definition as Definition from information_schema.views where table_schema=database() order by table_name asc;");
        }
        #endregion

        #region 获取当前数据库所有用户存储过程
        /// <summary>
        /// 获取当前数据库所有用户存储过程
        /// </summary>
        /// <returns></returns>
        public virtual List<string> GetProcedures()
        {
            return this.ExecuteDataTable(@"select name from mysql.proc where db = database() and `type` = 'PROCEDURE' order by name asc;").ToList<string>();
        }
        #endregion

        #region 获取当前表所有列
        /// <summary>
        /// 获取当前表所有列
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns></returns>
        public virtual List<DataColumns> GetColumns(string tableName)
        {
            List<DataColumns> DataColumns = new List<DataColumns>();
            this.ExecuteDataTable(@"select A.*,(select count(0) from information_schema.KEY_COLUMN_USAGE as B where B.TABLE_SCHEMA = database() and B.TABLE_NAME='{0}' and B.COLUMN_NAME=A.COLUMN_NAME) as IS_UNIQUE from information_schema.COLUMNS as A
where A.TABLE_NAME = '{0}' and A.TABLE_SCHEMA = database();".format(tableName)).Rows.Each<DataRow>(dr =>
            {
                string _CharLength = dr["CHARACTER_MAXIMUM_LENGTH"].ToString();
                _CharLength = _CharLength == "4294967295" ? "0" : _CharLength;
                string ColumnType = dr["COLUMN_TYPE"].ToString();
                int CharLength = _CharLength.ToCast<int>();
                int NumberLength = dr["NUMERIC_PRECISION"].ToCast<int>();
                string type = dr["DATA_TYPE"].ToString();
                if (ColumnType.EqualsIgnoreCase("tinyint(1) unsigned")) type = "Bit";
                var defaultValue = dr["COLUMN_DEFAULT"].ToString();
                if (defaultValue.IsMatch(@"hex"))
                {
                    defaultValue = "UUID";
                }
                else if (defaultValue.IsMatch(@"CURRENT_TIMESTAMP"))
                {
                    defaultValue = "NOW";
                }
                else if (defaultValue.IsMatch(@"CURRENT_TIMESTAMP"))
                {
                    defaultValue = "TIMESTAMP";
                }
                DataColumns.Add(new DataColumns
                {
                    Name = dr["COLUMN_NAME"].ToString(),
                    IsNull = dr["IS_NULLABLE"].ToString() == "YES",
                    DefaultValue = defaultValue,
                    Description = dr["COLUMN_COMMENT"].ToString(),
                    Digits = dr["NUMERIC_SCALE"].ToCast<int>(),
                    IsIdentity = dr["EXTRA"].ToString().ToLower() == "auto_increment",
                    Length = CharLength == 0 ? NumberLength : CharLength,
                    PrimaryKey = dr["COLUMN_KEY"].ToString() == "PRI",
                    SortID = dr["ORDINAL_POSITION"].ToCast<int>(),
                    Type = type,
                    IsUnique = dr["IS_UNIQUE"].ToString() == "1"
                });
            });
            return DataColumns;
        }
        /// <summary>
        /// 获取当前表所有列
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns></returns>
        public DataColumnCollection GetDataColumns(string tableName)
        {
            return this.ExecuteDataTable("select * from {0} limit 0,0".format(tableName)).Columns;
        }
        #endregion

        #region 查询数据
        /// <summary>
        /// 分页查询数据
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="Columns">显示列</param>
        /// <param name="Condition">条件</param>
        /// <param name="OrderColumnName">排序字段</param>
        /// <param name="OrderType">排序类型ASC或DESC</param>
        /// <param name="PageIndex">当前页</param>
        /// <param name="PageSize">一页多少条</param>
        /// <param name="PageCount">共多少页</param>
        /// <param name="Counts">共多少条</param>
        /// <param name="PrimaryKey">主键</param>
        /// <returns></returns>
        public override DataTable Select(string tableName, string Columns, string Condition, string OrderColumnName, string OrderType, int PageIndex, int PageSize, out int PageCount, out int Counts, string PrimaryKey = "")
        {
            PageCount = 1; Counts = 0;
            if (tableName == "") return new DataTable();
            Columns = Columns == "" ? "*" : Columns;
            if (Condition != "" && !Condition.IsMatch(@"^\s*where")) Condition = " where " + Condition;
            Counts = base.ExecuteScalar("select count(0) from {0}{1}".format(tableName, Condition)).ToString().ToInt32();
            PageSize = PageSize == 0 ? 10 : PageSize;
            PageIndex = PageIndex <= 0 ? 1 : PageIndex;
            if (!OrderColumnName.IsMatch(@"\s*order by")) OrderColumnName = "order by " + OrderColumnName;
            OrderColumnName += " " + OrderType;
            if (Counts == 0) return new DataTable();
            PageCount = Math.Ceiling(Convert.ToDouble(Counts) / Convert.ToDouble(PageSize)).ToCast<int>();
            PageIndex = PageIndex > PageCount ? PageCount : PageIndex;
            if (PrimaryKey == "")
                return base.ExecuteDataTable("select {0} from {1} {2} {3} limit {4},{5};".format(Columns, tableName, Condition, OrderColumnName, (PageIndex - 1) * PageSize, PageSize));
            else
                return base.ExecuteDataTable("select {0} from {1} A JOIN (select {2} from {1} {3} {4} limit {5},{6}) B ON A.{2} = B.{2};".format(Columns, tableName, PrimaryKey, Condition, OrderColumnName, (PageIndex - 1) * PageSize, PageSize));
        }
        /// <summary>
        /// 分页查询数据
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="tableName">表名</param>
        /// <param name="Columns">显示列</param>
        /// <param name="Condition">条件</param>
        /// <param name="OrderColumnName">排序字段</param>
        /// <param name="OrderType">排序类型ASC或DESC</param>
        /// <param name="PageIndex">当前页</param>
        /// <param name="PageSize">一页多少条</param>
        /// <param name="PageCount">共多少页</param>
        /// <param name="Counts">共多少条</param>
        /// <param name="PrimaryKey">主键</param>
        /// <returns></returns>
        public override List<T> Select<T>(string tableName, string Columns, string Condition, string OrderColumnName, string OrderType, int PageIndex, int PageSize, out int PageCount, out int Counts, string PrimaryKey = "")
        {
            return this.Select(tableName, Columns, Condition, OrderColumnName, OrderType, PageIndex, PageSize, out PageCount, out Counts).ToList<T>();
        }
        #endregion

        #region 创建数据库表
        /// <summary>
        /// 创建数据库表
        /// </summary>
        /// <param name="modelType">表model类型</param>
        /// <returns></returns>
        public virtual Boolean CreateTable(Type modelType)
        {
            /*DROP TABLE IF EXISTS `aaa`;
CREATE TABLE `aaa` (
  `ID` int(11) NOT NULL AUTO_INCREMENT COMMENT 'ID',
  `UserName` varchar(30) DEFAULT NULL COMMENT '用户名称',
  `UserAccount` varchar(30) NOT NULL DEFAULT '' COMMENT '用户帐号',
  `UserPwd` varchar(50) DEFAULT '' COMMENT '用户密码',
  `Sex` varchar(10) DEFAULT '男' COMMENT '性别',
  `Pass` bit(1) DEFAULT b'0' COMMENT '是否显示',
  `Birthday` datetime DEFAULT '0000-00-00 00:00:00' COMMENT '出生日期',
  `CreateDate` timestamp NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `Address` varchar(200) DEFAULT '河南省' COMMENT '地址',
  PRIMARY KEY (`ID`),
  KEY `UserAccount` (`UserAccount`,`UserPwd`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='测试表名';*/

            string SQLString = @"
DROP TABLE IF EXISTS `{0}`;
CREATE TABLE `{0}` (
   {1}
   {2}
   {3}
)ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='{4}';
select 1;
";
            TableAttribute Table = modelType.GetTableAttribute();
            Table = Table ?? new TableAttribute();
            string Fields = "", Indexs = "", PrimaryKey = "";
            Table.Name = (Table.Name == null || Table.Name.IsNullOrEmpty()) ? modelType.Name : Table.Name;
            DataType dType = new DataType(this.ProviderType);
            modelType.GetProperties(BindingFlags.Public | BindingFlags.IgnoreCase| BindingFlags.Instance).Each(p =>
            {
                if (",ConnectionString,ConnectionTimeOut,CommandTimeOut,ProviderType,IsTransaction,ErrorMessage,tableName,QueryableX,".IndexOf("," + p.Name + ",") == -1)
                {
                    ColumnAttribute Column = p.GetColumnAttribute();
                    Column = Column ?? new ColumnAttribute { AutoIncrement = false, IsIndex = false, IsNullable = true, IsUnique = false, PrimaryKey = false, Length = 0, DefaultValue = "" };
                    Column.Name = (Column.Name == null || Column.Name.IsNullOrEmpty()) ? p.Name : Column.Name;
                    Column.DataType = Column.DataType.IsNullOrEmpty() ? dType[p.PropertyType] : Column.DataType;
                    if (Column.AutoIncrement && Column.PrimaryKey)
                    {
                        string type = "int", DefaultValue = "";
                        if (Column.DataType.ToString() == "bigint" || Column.DataType.ToString() == "int")
                        {
                            type = "int(11)";
                            DefaultValue = "AUTO_INCREMENT";
                        }
                        else if (Column.DataType.ToString() == "uniqueidentifier")
                        {
                            type = "STRING";
                            DefaultValue = "";
                        }
                        else
                        {
                            type = Column.DataType.ToString();
                            DefaultValue = "DEFAULT '{0}' ".format(Column.DefaultValue);
                        }
                        Fields += "`{0}` {1} NOT NULL {2} COMMENT '{3}',".format(Column.Name, type, DefaultValue, Column.Description);
                    }
                    else
                    {
                        string DefaultValue = Column.DefaultValue.ToString();
                        if (DefaultValue == "now()" || DefaultValue == "getdate()") DefaultValue = "0000-00-00 00:00:00";
                        else if (Column.Name.ToLower().IndexOf("timestamp") > -1)
                        {
                            DefaultValue = "CURRENT_TIMESTAMP";
                        }
                        var FieldType = dType[p.PropertyType.GetBaseType()];
                        Fields += String.Format(@"
                        `{0}` {1}{2}{3},",
                        Column.Name,
                        FieldType,
                        ((Column.Length == 0 || ",datetime,".IndexOf("," + FieldType.ToString().ToLower() + ",") > -1) ? " " : ("(" + Column.Length + ") ")) + ((Column.IsNullable && !Column.PrimaryKey) ? "NULL" : "NOT NULL") + (Column.DefaultValue.IsNullOrEmpty() ? "" : (" DEFAULT " + (DefaultValue.IsNumberic() ? Column.DefaultValue : ((DefaultValue.StartsWith("'") && DefaultValue.EndsWith("'")) || DefaultValue.ToLower() == "datetime('now','localtime')" || Column.Name.ToLower().IndexOf("timestamp") > -1) ? DefaultValue : ("'" + DefaultValue + "'")) + "")),
                         " COMMENT '{0}'".format(Column.Description));
                    }
                    if (Column.PrimaryKey)
                    {
                        PrimaryKey = ",PRIMARY KEY (`{0}`)".format(Column.Name);
                    }
                    if (Column.IsIndex)
                    {
                        Indexs += ",`{0}`".format(Column.Name);
                    }
                }
            });
            Indexs = Indexs.TrimEnd(',');
            if (Indexs.IsNotNullOrEmpty())
            {
                Indexs = "KEY `{0}Index` ({1})".format(Table.Name, Indexs);
            }
            return base.ExecuteScalar(SQLString.format(Table.Name, Fields.TrimEnd(','), PrimaryKey, Indexs, Table.Description.IsNullOrEmpty() ? Table.Name : Table.Description)).ToString().ToInt16() == 1;
        }
        /// <summary>
        /// 创建数据库表 属性用 TableAttribute,ColumnAttribute
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <returns></returns>
        public virtual Boolean CreateTable<T>()=> CreateTable(typeof(T));
        #endregion

        #endregion

        #region 析构器
        /// <summary>
        /// 析构器
        /// </summary>
        ~MySqlHelper() { }
        #endregion
    }
}