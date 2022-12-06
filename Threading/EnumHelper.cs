﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
/****************************************************************
*  Copyright © (2017) www.fayelf.com All Rights Reserved.       *
*  Author : jacky                                               *
*  QQ : 7092734                                                 *
*  Email : jacky@fayelf.com                                     *
*  Site : www.fayelf.com                                        *
*  Create Time : 2017-10-31 14:18:38                            *
*  Version : v 1.0.0                                            *
*  CLR Version : 4.0.30319.42000                                *
*****************************************************************/
namespace XiaoFeng.Threading
{
    /// <summary>
    /// 定时器类型
    /// </summary>
    public enum TimerType
    {
        /// <summary>
        /// 一次
        /// </summary>
        [Description("一次")]
        Once = 0,
        /// <summary>
        /// 每时
        /// </summary>
        [Description("每时")]
        Hour = 1,
        /// <summary>
        /// 每天
        /// </summary>
        [Description("每天")]
        Day = 2,
        /// <summary>
        /// 每周
        /// </summary>
        [Description("每周")]
        Week = 3,
        /// <summary>
        /// 每月
        /// </summary>
        [Description("每月")]
        Month = 4,
        /// <summary>
        /// 每年
        /// </summary>
        [Description("每年")]
        Year = 5,
        /// <summary>
        /// 间隔
        /// </summary>
        [Description("间隔")]
        Interval = 6
    }
}
