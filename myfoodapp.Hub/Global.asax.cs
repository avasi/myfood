﻿using Microsoft.AspNet.Identity;
using myfoodapp.Hub.Business;
using myfoodapp.Hub.Common;
using myfoodapp.Hub.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Timers;
using System.Web;
using System.Web.Configuration;
using System.Web.Helpers;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace myfoodapp.Hub
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        private static double RulesTimerIntervalInMilliseconds = Convert.ToDouble(WebConfigurationManager.AppSettings["rulesTimerIntervalInMilliseconds"]);
        private static double OfflineTimerIntervalInMilliseconds = Convert.ToDouble(WebConfigurationManager.AppSettings["offlineTimerIntervalInMilliseconds"]);

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            // Disable the HTTP Header X-Frame-Options: SAMEORIGIN
            AntiForgeryConfig.SuppressXFrameOptionsHeader = true;

            GlobalConfiguration.Configuration.MessageHandlers.Add(new AuthorizationHeaderHandler());

            // Change the URL localization scheme from Scheme1.
            i18n.UrlLocalizer.UrlLocalizationScheme = i18n.UrlLocalizationScheme.Void;

            // Specifies a custom method called after a nugget has been translated
            // that allows the resulting message to be modified, for instance according to content type.
            i18n.LocalizedApplication.Current.TweakMessageTranslation = delegate (System.Web.HttpContextBase context, i18n.Helpers.Nugget nugget, i18n.LanguageTag langtag, string message)
            {
                switch (context.Response.ContentType)
                {
                    case "text/html":
                        return message.Replace("\'", "&apos;");
                }
                return message;
            };

            // Blacklist certain URLs from being 'localized' via a callback.
            i18n.UrlLocalizer.IncomingUrlFilters += delegate (Uri url)
            {
                if (url.LocalPath.EndsWith("sitemap.xml", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                return true;
            };

            System.Timers.Timer rulesTimer = new System.Timers.Timer(RulesTimerIntervalInMilliseconds);
            rulesTimer.Enabled = true;
            rulesTimer.Elapsed += new ElapsedEventHandler(rulesTimer_Elapsed);
            rulesTimer.Start();

            System.Timers.Timer offineTimer = new System.Timers.Timer(OfflineTimerIntervalInMilliseconds);
            offineTimer.Enabled = true;
            offineTimer.Elapsed += new ElapsedEventHandler(offlineTimer_Elapsed);
            offineTimer.Start();
        }

        static void offlineTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            DateTime MyScheduledRunTime = DateTime.Parse(WebConfigurationManager.AppSettings["timerStartTime"]);
            DateTime CurrentSystemTime = DateTime.Now;
            DateTime LatestRunTime = MyScheduledRunTime.AddMilliseconds(RulesTimerIntervalInMilliseconds);
            if ((CurrentSystemTime.CompareTo(MyScheduledRunTime) >= 0) && (CurrentSystemTime.CompareTo(LatestRunTime) <= 0))
            {
                var db = new ApplicationDbContext();

                var upRunningStatus = db.ProductionUnitStatus.Where(s => s.Id == 3).FirstOrDefault();
                var offlineStatus = db.ProductionUnitStatus.Where(s => s.Id == 6).FirstOrDefault();

                var upRunningProductionUnits = db.ProductionUnits.Include(p => p.owner.language).Where(p => p.productionUnitStatus.Id == upRunningStatus.Id).ToList();

                var currentDate = DateTime.Now;

                upRunningProductionUnits.ForEach(p =>
                {
                    if (p.lastMeasureReceived == null ||  currentDate - p.lastMeasureReceived > TimeSpan.FromMinutes(30))
                        p.productionUnitStatus = offlineStatus;
                        if (p.owner.notificationPushKey != null)
                        {
                            NotificationPushManager.PioneerUnitOfflineMessage(p);
                        }
                });

                db.SaveChanges();
           }
        }

        static void rulesTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            DateTime MyScheduledRunTime = DateTime.Parse(WebConfigurationManager.AppSettings["timerStartTime"]);
            DateTime CurrentSystemTime = DateTime.Now;
            DateTime LatestRunTime = MyScheduledRunTime.AddMilliseconds(RulesTimerIntervalInMilliseconds);

            if ((CurrentSystemTime.CompareTo(MyScheduledRunTime) >= 0) && (CurrentSystemTime.CompareTo(LatestRunTime) <= 0))
            {
                PerformValidationRules();
                SendDailyMessage();
            }
        }

        protected void Application_AuthenticateRequest()
        {
            if (HttpContext.Current.User != null)
            {
                var db = new ApplicationDbContext();
                var currentUser = HttpContext.Current.User.Identity.GetUserName();

                var currentProductionUnitOwner = db.ProductionUnitOwners
                                                               .Include(p => p.user)
                                                               .Include(p => p.language)
                                                               .Where(p => p.user.UserName == currentUser).FirstOrDefault();
                if (currentProductionUnitOwner != null && currentProductionUnitOwner.language != null)
                {
                    i18n.HttpContextExtensions.SetPrincipalAppLanguageForRequest(
                    System.Web.HttpContext.Current,
                    i18n.LanguageHelpers.GetMatchingAppLanguage(currentProductionUnitOwner.language.description), true);
                }
            }
            else if (HttpContext.Current.User == null && (HttpContext.Current.Request.UrlReferrer != null || !String.IsNullOrEmpty(Request.QueryString["lang"])))
            {
                if (Request.QueryString["lang"] == "fr" || Request.QueryString["lang"] == "en" || Request.QueryString["lang"] == "lu" || Request.QueryString["lang"] == "fl")
                {
                    i18n.HttpContextExtensions.SetPrincipalAppLanguageForRequest(
                    System.Web.HttpContext.Current,
                    i18n.LanguageHelpers.GetMatchingAppLanguage(Request.QueryString["lang"]), true);
                }
                else if ((HttpUtility.ParseQueryString(HttpContext.Current.Request.UrlReferrer.Query).Get("lang") != String.Empty))
                {
                    var strLang = HttpUtility.ParseQueryString(HttpContext.Current.Request.UrlReferrer.Query).Get("lang");

                    if (strLang == "fr" || strLang == "en" || strLang == "lu" || strLang == "fl")
                    {
                        i18n.HttpContextExtensions.SetPrincipalAppLanguageForRequest(
                        System.Web.HttpContext.Current,
                        i18n.LanguageHelpers.GetMatchingAppLanguage(strLang));
                    }
                }
            }
        }

        private static void SendDailyMessage()
        {
            ApplicationDbContext db = new ApplicationDbContext();

            var yesterdayDate = DateTime.Now.AddDays(-1);

            var todayEvents = db.Events.Include(e => e.productionUnit.owner.user)
                                       .Include(e => e.productionUnit.owner.language)
                                       .Where(ev => ev.date > yesterdayDate).ToList();

            var groupedEvents = todayEvents.GroupBy(ev => ev.productionUnit);

            foreach (var item in groupedEvents)
            {
                var currentProductionUnit = item.Key;
                if(currentProductionUnit.owner.notificationPushKey != null)
                {
                    NotificationPushManager.PioneerUnitEventMessage(currentProductionUnit);
                } 
            }
        }

        private static void PerformValidationRules()
        {
            var db = new ApplicationDbContext();
            var dbLog = new ApplicationDbContext();

            var upRunningStatus = db.ProductionUnitStatus.Where(p => p.Id == 3).FirstOrDefault();

            var upRunningProductionUnits = db.ProductionUnits.Include(p => p.productionUnitStatus)
                                                             .Where(p => p.productionUnitStatus.Id == upRunningStatus.Id).ToList();

            dbLog.Logs.Add(Log.CreateLog(String.Format("Rules Processing starts @{0} for {1} Production Units", DateTime.Now, upRunningProductionUnits.Count), Log.LogType.Information));
            dbLog.SaveChanges();

            upRunningProductionUnits.ForEach(p =>
            {
                dbLog.Logs.Add(Log.CreateLog(String.Format("Measures Processing for {0}", p.info), Log.LogType.Information));
                dbLog.SaveChanges();

                var currentMeasures = AquaponicsRulesManager.MeasuresProcessor(p.Id);

                dbLog.Logs.Add(Log.CreateLog(String.Format("Measures Validating for {0}", p.info), Log.LogType.Information));
                dbLog.SaveChanges();

                try
                {
                    AquaponicsRulesManager.ValidateRules(currentMeasures, p.Id);
                }
                catch (Exception ex)
                {
                    dbLog.Logs.Add(Log.CreateErrorLog(String.Format("Error with Rule Manager Evaluator"), ex));
                    dbLog.SaveChanges();
                }

                dbLog.Logs.Add(Log.CreateLog(String.Format("Rules Validation ended for {0}", p.info), Log.LogType.Information));
                dbLog.SaveChanges();
            });
        }

    }
}
