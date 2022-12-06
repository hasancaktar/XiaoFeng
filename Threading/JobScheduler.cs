﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Security;
using System.Collections.Concurrent;
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
    /// 作业调度器
    /// Version : 2.0
    /// </summary>
    public class JobScheduler : Disposable, IJobScheduler
    {
        #region 构造器
        /// <summary>
        /// 无参构造器
        /// </summary>
        public JobScheduler()
        {
            
        }
        /// <summary>
        /// 设置带名称的调度
        /// </summary>
        /// <param name="name">名称</param>
        public JobScheduler(string name) : this()
        {
            this.Name = name;
        }
        #endregion

        #region 属性
        /// <summary>
        /// 调度器集合
        /// </summary>
        public static readonly ConcurrentDictionary<string, JobScheduler> Schedulers = new ConcurrentDictionary<string, JobScheduler>();
        /// <summary>
        /// 调度名称
        /// </summary>
        public string Name { get; set; } = "Default";
        /// <summary>
        /// 间隔时长 单位为毫秒
        /// </summary>
        private int Period { get; set; } = 1 * 60 * 60 * 1000;
        /// <summary>
        /// 调度作业列表
        /// </summary>
        private readonly ConcurrentDictionary<Guid, IJob> SchedulerJobs = new ConcurrentDictionary<Guid, IJob>();
        /// <summary>
        /// 消费运行状态
        /// </summary>
        private Boolean ConsumeState { get; set; } = false;
        /// <summary>
        /// 取消信号
        /// </summary>
        public CancellationTokenSource CancelToken { get; set; } = new CancellationTokenSource();
        /// <summary>
        /// 线程同步信号
        /// </summary>
        private ManualResetEventSlim Manual = new ManualResetEventSlim(false);
        /// <summary>默认调度器</summary>
        public static IJobScheduler Default { get; } = CreateScheduler("Default");
        /// <summary>
        /// 当前调度器
        /// </summary>
        [ThreadStatic]
        private IJobScheduler _Current = null;
        /// <summary>当前调度器</summary>
        public IJobScheduler Current { get { return _Current ?? CreateScheduler(); } private set { _Current = value; } }
        #endregion

        #region 方法

        #region 创建调度
        /// <summary>
        /// 创建调度
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public static JobScheduler CreateScheduler(string name = "Default")
        {
            if (Schedulers.TryGetValue(name, out var ts)) return ts;
            return Synchronized.Run(() =>
            {
                ts = new JobScheduler(name);
                Schedulers.TryAdd(name, ts);
                LogHelper.Info("创建调度器:" + name);
                return ts;
            });
        }
        #endregion

        #region 添加作业
        /// <summary>
        /// 添加多个作业
        /// </summary>
        /// <param name="jobs">作业集</param>
        public async Task Add(params IJob[] jobs)
        {
            await this.AddRange(jobs);
        }
        /// <summary>
        /// 添加作业
        /// </summary>
        /// <param name="job">作业</param>
        public async Task Add(IJob job)
        {
            if (job == null || this.SchedulerJobs.ContainsKey(job.ID)) return;
            var j = this.SchedulerJobs.Values.Where(a => a.Name == job.Name);
            //如果作业中有正在等待的作业并且名和要加入的作业名称一样的情况下则不再添加作业
            if (j.Any() && j != null && j.First().Status == JobStatus.Waiting)
            {
                return;
            }
            if (job.StartTime.HasValue)
            {
                var startTime = job.StartTime.Value;
                job.StartTime = null;
                if ((startTime - DateTime.Now).TotalMilliseconds > 100)
                {
                    job.NextTime = startTime;
                    job.StartTime = null;
                }
            }
            job.Status = JobStatus.Waiting;
            this.SchedulerJobs.TryAdd(job.ID, job);
            this.Wake();
            await Task.CompletedTask;
        }
        /// <summary>
        /// 批量添加作业
        /// </summary>
        /// <param name="jobs">作业集</param>
        public async Task AddRange(IEnumerable<IJob> jobs)
        {
            if (jobs.Count() == 0) return;
            foreach (var j in jobs)
                await this.Add(j);
        }
        #endregion

        #region 移除作业
        /// <summary>
        /// 移除作业
        /// </summary>
        /// <param name="name">作业名称</param>
        public void Remove(string name)
        {
            if (name.IsNullOrEmpty()) return;
            var job = this.SchedulerJobs.Values.First(a => a.Name.EqualsIgnoreCase(name));
            if (job == null) return;
            this.Remove(job);
        }
        /// <summary>
        /// 移除作业
        /// </summary>
        /// <param name="ID">ID</param>
        public void Remove(Guid ID)
        {
            if (ID == Guid.Empty) return;
            this.SchedulerJobs.TryRemove(ID, out var job);
            if (job != null) job.CancelToken.Cancel();
            this.Wake();
        }
        /// <summary>
        /// 移除作业
        /// </summary>
        /// <param name="job">作业</param>
        public void Remove(IJob job)
        {
            if (job == null) return;
            this.Remove(job.ID);
        }
        #endregion

        #region 唤醒处理
        /// <summary>唤醒处理</summary>
        [SecuritySafeCritical]
        public void Wake()
        {
            Manual.Set();
            Synchronized.Run(() =>
            {
                if (this.CancelToken.IsCancellationRequested || !this.ConsumeState)
                {
                    this.CancelToken = new CancellationTokenSource();
                    this.CancelToken.Token.Register(() =>
                    {
                        Synchronized.Run(() =>
                        {
                            this.ConsumeState = false;
                        });
                    });
                    this.ConsumeState = true;
                    //Console.ForegroundColor = ConsoleColor.Magenta;
                    LogHelper.Warn($"-- 有新作业任务,启动调度器[{this.Name}]. --");
                    //Console.ResetColor();
                    this.Process();
                }
            });
            /*var e = this.WaitForTimer;
            if (e != null)
            {
                var swh = e.SafeWaitHandle;
                if (swh != null && !swh.IsClosed) e.Set();
            }*/
        }
        #endregion

        #region 入口
        /// <summary>
        /// 入口
        /// </summary>
        public void Process()
        {
            Current = this;
            new Task(() =>
            {
                if (this.Name.IsNotNullOrEmpty())
                    Thread.CurrentThread.Name = this.Name;
                while (!this.CancelToken.IsCancellationRequested)
                {
                    if (this.SchedulerJobs.Count == 0)
                    {
                        this.Period = 1 * 60 * 60 * 1000;
                        this.CancelToken.Cancel();
                        //this.MainTask = null;
                        //Console.ForegroundColor = ConsoleColor.Magenta;
                        LogHelper.Warn($"-- 暂无作业任务,终止调度器[{this.Name}]. --");
                        //Console.ResetColor();
                        break;
                    }
                    var now = DateTime.Now;
                    /*转换成并行计算*/
                    //Parallel.ForEach(this.SchedulerJobs.Values, job =>
                    this.SchedulerJobs.Values.Each(job =>
                    {
                        int period = 0;
                        if (job.Status == JobStatus.Waiting && this.CheckTime(job, now, out period))
                        {
                            if (job.TimerType != TimerType.Once)
                                Synchronized.Run(() =>
                                {
                                    this.Period = Math.Min(this.Period, job.Period);
                                });
                            job.Status = JobStatus.Runing;
                            job.LastTime = now;
                            LogHelper.Warn($"开始运行作业 {job.Name} - {this.SchedulerJobs.Count} - {job.NextTime:yyyy-MM-dd HH:mm:ss.ffff}");
                            if (job.CompleteCallBack == null) job.Status = JobStatus.Waiting;
                            if (!job.Async)
                                Execute(job);
                            else
                            {
                                /*
                                 * Date:2022-04-01
                                 * 优化调度执行完成后再间隔时间
                                 */
                                //new Task(this.Execute, job, CancellationTokenSource.CreateLinkedTokenSource(this.CancelToken.Token, job.CancelToken.Token).Token, TaskCreationOptions.LongRunning).Start();
                                Task.Factory.StartNew(this.Execute, job, CancellationTokenSource.CreateLinkedTokenSource(this.CancelToken.Token, job.CancelToken.Token).Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ContinueWith((t, j) =>
                                {
                                    var _job = (IJob)j;
                                    if (_job.CompleteCallBack != null)
                                        _job.Status = JobStatus.Waiting;
                                }, job, CancellationTokenSource.CreateLinkedTokenSource(this.CancelToken.Token, job.CancelToken.Token).Token);
                            }
                        }
                        else
                        {
                            if (period > 0)
                                Synchronized.Run(() =>
                                {
                                    this.Period = Math.Min(this.Period, period);
                                });
                        }
                    });
                    if (this.Manual == null) this.Manual = new ManualResetEventSlim(false);
                    Manual.Reset();
                    Manual.Wait(TimeSpan.FromMilliseconds(this.Period < 0 ? 0 : this.Period));
                }
            }, CancelToken.Token, TaskCreationOptions.LongRunning).Start();
        }
        #endregion

        #region 执行作业
        /// <summary>
        /// 执行作业
        /// </summary>
        /// <param name="state">作业</param>
        [SecuritySafeCritical]
        private void Execute(object state)
        {
            var job = state as IJob;
            try
            {
                if (job.SuccessCallBack == null)
                {
                    if (job.CompleteCallBack == null)
                    {
                        this.Remove(job);
                        return;
                    }
                    else
                        job.SuccessCallBack = j => j.CompleteCallBack?.Invoke(j);
                }
                job.SuccessCallBack?.Invoke(job);
                this.Success(job);
            }
            catch (ThreadAbortException ex)
            {
                LogHelper.Error(ex, "任务终止");
                job.FailureCallBack?.Invoke(job, ex);
                this.Failure(job);
            }
            catch (ThreadInterruptedException ex)
            {
                LogHelper.Error(ex, "任务中断");
                job.FailureCallBack?.Invoke(job, ex);
                this.Failure(job);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex);
                job.FailureCallBack?.Invoke(job, ex);
                this.Failure(job);
            }
            finally
            {
                //job.Status = JobStatus.Wait;
            }
        }
        #endregion

        #region 执行成功后执行
        /// <summary>
        /// 执行成功后执行
        /// </summary>
        /// <param name="job">作业</param>
        private void Success(IJob job)
        {
            job.Message.Add("执行作业[{0}]成功.[{1}]".format(job.Name, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")));
            LogHelper.Warn($"执行作业 {job.Name} 完成.");
            job.SuccessCount++;
            if (job.TimerType == TimerType.Once || job.IsDestroy || (job.MaxCount.HasValue && job.SuccessCount + job.FailureCount >= job.MaxCount))
            {
                this.Remove(job.ID);
            }
        }
        #endregion

        #region 执行失败后执行
        /// <summary>
        /// 执行失败后执行
        /// </summary>
        /// <param name="job">作业</param>
        private void Failure(IJob job)
        {
            job.Message.Add("执行作业[{0}]失败.[{1}]".format(job.Name, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")));
            job.FailureCount++;
            if (job.TimerType == TimerType.Once || job.IsDestroy || (job.MaxCount.HasValue && job.SuccessCount + job.FailureCount >= job.MaxCount))
            {
                this.Remove(job.ID);
            }
        }
        #endregion

        #region 检查定时器是否到期
        /// <summary>检查定时器是否到期</summary>
        /// <param name="job"></param>
        /// <param name="now"></param>
        /// <param name="period">返回间隔</param>
        /// <returns></returns>
        private Boolean CheckTime(IJob job, DateTime now, out int period)
        {
            period = -1;
            if (job.Status == JobStatus.Runing) return false;
            if ((job.MaxCount.HasValue && job.Count >= job.MaxCount) ||
            (job.ExpireTime.HasValue && job.ExpireTime < now))
            {
                this.Remove(job);
                return false;
            }
            if (job.TimerType == TimerType.Interval)
            {
                if (job.NextTime.HasValue)
                {
                    if (job.NextTime <= now)
                    {
                        job.NextTime = now.AddMilliseconds(job.Period);
                        return true;
                    }
                    period = (int)(job.NextTime.Value - now).TotalMilliseconds;
                    if (period <= 1000) return true;
                    return false;
                }
                else
                {
                    job.Period = job.Period > 0 ? job.Period : job.Time.TotalSeconds * 1000;
                    job.NextTime = now.AddMilliseconds(job.Period);
                    return true;
                }
            }
            else if (job.TimerType == TimerType.Once)
            {
                if (job.NextTime.HasValue)
                {
                    period = (int)(job.NextTime.Value - now).TotalMilliseconds;
                    job.Period = period;
                    return period <= 1000;
                }
                return true;
            }
            else if (job.TimerType == TimerType.Hour)
            {
                var _now = new Time(now);
                var TimeStamp = job.Time.TotalSeconds - _now.TotalSeconds;
                if (TimeStamp < 0)
                {
                    TimeStamp += 1 * 60 * 60;
                }
                else if (TimeStamp <= 3)
                {
                    job.NextTime = "{0} {1}".format(now.ToString("yyyy-MM-dd"), job.Time.ToString()).ToCast<DateTime>().AddHours(1);
                    job.Period = 1 * 60 * 60 * 1000;
                    return true;
                }
                job.Period = TimeStamp * 1000;
                job.NextTime = now.AddMilliseconds(job.Period);
                return false;
            }
            else if (job.TimerType == TimerType.Day)
            {
                var _now = new Time(now);
                if (job.DayOrWeekOrHour == null || job.DayOrWeekOrHour.Length == 0)
                {
                    var TimeStamp = job.Time.TotalSeconds - _now.TotalSeconds;
                    if (TimeStamp < 0)
                    {
                        TimeStamp += 24 * 60 * 60;
                    }
                    else if (TimeStamp <= 3)
                    {
                        job.NextTime = "{0} {1}".format(now.ToString("yyyy-MM-dd"), job.Time.ToString()).ToCast<DateTime>().AddDays(1);
                        job.Period = 24 * 60 * 60 * 1000;
                        return true;
                    }
                    job.Period = TimeStamp * 1000;
                    job.NextTime = now.AddMilliseconds(job.Period);
                    return false;
                }
                else
                {
                    var index = this.GetIndex(job.DayOrWeekOrHour, _now.Hour, TimerType.Day);
                    if (index >= 0)
                    {
                        var Hour = job.DayOrWeekOrHour[index];
                        var TimeStamp = new Time(job.Time) { Hour = Hour }.TotalSeconds - _now.TotalSeconds;
                        if (TimeStamp < 0)
                        {
                            TimeStamp += 24 * 60 * 60;
                        }
                        else if (TimeStamp <= 3)
                        {
                            if (index < job.DayOrWeekOrHour.Length - 1)
                                index++;
                            else
                            {
                                index = 0;
                                now = now.AddDays(1);
                            }
                            job.NextTime = "{0} {1}".format(now.ToString("yyyy-MM-dd"), new Time(job.Time) { Hour = job.DayOrWeekOrHour[index] }.ToString()).ToCast<DateTime>();
                            job.Period = (int)(job.NextTime.Value - now).TotalMilliseconds;
                            return true;
                        }
                        job.Period = TimeStamp * 1000;
                        job.NextTime = now.AddMilliseconds(job.Period);
                        return false;
                    }
                    else
                    {
                        this.Remove(job);
                        return false;
                    }
                }
            }
            else if (job.TimerType == TimerType.Week)
            {
                var CurrentWeek = (int)now.DayOfWeek;
                if (job.DayOrWeekOrHour == null || job.DayOrWeekOrHour.Length == 0)
                {
                    this.Remove(job);
                    return false;
                }
                else
                {
                    var index = this.GetIndex(job.DayOrWeekOrHour, CurrentWeek, TimerType.Week);
                    if (index >= 0)
                    {
                        var Week = job.DayOrWeekOrHour[index];
                        var WeekStamp = Week - CurrentWeek;
                        /*if (WeekStamp < 0)
                        {
                            WeekStamp += 7;
                        }
                        else */
                        if (WeekStamp >= 0 && WeekStamp <= 6)
                        {
                            if (index < job.DayOrWeekOrHour.Length - 1)
                                index++;
                            else
                            {
                                index = 0;
                                now = now.AddDays(7 - job.DayOrWeekOrHour[index]);
                            }
                            job.NextTime = "{0} {1}".format(now.ToString("yyyy-MM-dd"), new Time(job.Time) { Hour = job.DayOrWeekOrHour[index] }.ToString()).ToCast<DateTime>();
                            job.Period = (int)(job.NextTime.Value - now).TotalMilliseconds;
                            return true;
                        }
                        job.NextTime = now.AddMilliseconds(job.Period);
                        return false;
                    }
                    else
                    {
                        this.Remove(job);
                        return false;
                    }
                }
            }
            else if (job.TimerType == TimerType.Month)
            {
                var _now = new Time(now);
                if (job.DayOrWeekOrHour == null || job.DayOrWeekOrHour.Length == 0)
                {
                    var TimeStamp = job.Time.TotalSeconds - _now.TotalSeconds;
                    if (TimeStamp < 0)
                    {
                        TimeStamp += 24 * 60 * 60;
                    }
                    else if (TimeStamp <= 3)
                    {
                        job.NextTime = "{0} {1}".format(now.ToString("yyyy-MM-dd"), job.Time.ToString()).ToCast<DateTime>().AddDays(1);
                        job.Period = 24 * 60 * 60 * 1000;
                        return true;
                    }
                    job.Period = TimeStamp * 1000;
                    job.NextTime = now.AddMilliseconds(job.Period);
                    return false;
                }
                else
                {
                    var index = this.GetIndex(job.DayOrWeekOrHour, now.Day, TimerType.Month);
                    if (index >= 0)
                    {
                        //var Hour = job.DayOrWeekOrHour[index];
                        var TimeStamp = (int)(now.AddDays(index - now.Day) - "{0} {1}".format(now.ToString("yyyy-MM-dd"), job.Time.ToString()).ToCast<DateTime>()).TotalSeconds;
                        var DaysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
                        if (TimeStamp < 0)
                        {
                            TimeStamp += 24 * 60 * 60;
                        }
                        else if (TimeStamp <= DaysInMonth)
                        {
                            if (index < job.DayOrWeekOrHour.Length - 1)
                                index++;
                            else
                            {
                                index = 0;
                                now = now.AddMonths(1);
                            }
                            var day = job.DayOrWeekOrHour[index];
                            if (day == 0) day = 1;
                            else if (day == -1) day = DaysInMonth;
                            job.NextTime = "{0}{1} {2}".format(now.ToString("yyyy-MM-"), day, job.Time.ToString()).ToCast<DateTime>();
                            job.Period = (int)(job.NextTime.Value - now).TotalMilliseconds;
                            return true;
                        }
                        job.Period = TimeStamp * 1000;
                        job.NextTime = now.AddMilliseconds(job.Period);
                        return false;
                    }
                    else
                    {
                        this.Remove(job);
                        return false;
                    }
                }
            }
            return false;
        }
        #endregion

        #region 获取离当前值最接近的索引
        /// <summary>
        /// 获取离当前值最接近的索引
        /// </summary>
        /// <param name="arr">数组</param>
        /// <param name="val">值</param>
        /// <param name="timerType">类型</param>
        /// <returns></returns>
        private int GetIndex(int[] arr, int val, TimerType timerType)
        {
            int Index = -1, Val = -1;
            if (arr == null || arr.Length == 0) return Index;
            for (int i = 0; i < arr.Length; i++)
            {
                var ArrValue = arr[i];
                if (ArrValue < 0)
                    ArrValue = GetMaxValue(timerType, ArrValue);
                var v = ArrValue - val;
                if (v >= 0 && v <= Val)
                {
                    Index = i; Val = v;
                }
            }
            return Index == -1 ? 0 : Index;
        }
        #endregion

        #region 获取最大值
        /// <summary>
        /// 获取最大值
        /// </summary>
        /// <param name="timerType">定时器类型</param>
        /// <param name="addValue">添加值</param>
        /// <returns></returns>
        public int GetMaxValue(TimerType timerType, int addValue)
        {
            if (addValue > -1) return addValue;
            addValue++;
            if (timerType == TimerType.Day)
                return 23 + addValue % 24;
            else if (timerType == TimerType.Week)
                return 6 + addValue % 7;
            else if (timerType == TimerType.Month)
            {
                var now = DateTime.Now;
                var MaxDays = DateTime.DaysInMonth(now.Year, now.Month);
                return MaxDays + addValue % MaxDays;
            }
            else return Math.Abs(--addValue);
        }
        #endregion

        #region 停止调度器
        /// <summary>
        /// 停止调度器
        /// </summary>
        public void Stop() => this.CancelToken.Cancel();
        #endregion

        #region 作业列表
        /// <summary>
        /// 作业列表
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IJob> GetJobs() => this.SchedulerJobs.Values;
        #endregion

        #endregion
    }
}