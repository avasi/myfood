﻿using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using myfoodapp.Hub.Models;
using myfoodapp.Hub.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Web.Mvc;
using myfoodapp.Hub.Business;
using System.Globalization;
using System.Threading;
using System.Web;
using i18n;

namespace myfoodapp.Hub.Controllers
{
    public class InteractiveMapController : Controller
    {
        public ActionResult Index(string lang)
        {
            ViewBag.Title = "Interactive Map Page";

            if(lang != String.Empty)
                System.Web.HttpContext.Current.Session["UserLang"] = lang;

            //var db = new ApplicationDbContext();
            //var measureService = new MeasureService(db);

            //var listMarker = new List<Marker>();

            //db.ProductionUnits.Include(p => p.owner).ToList().ForEach(p => 
            //                            listMarker.Add(new Marker(p.locationLatitude, p.locationLongitude, String.Format("{0} </br> start since {1}", 
            //                                                      p.info, p.startDate.ToShortDateString())) { shape = "redMarker" }));

            //var map = new Models.Map()
            //{
            //    Name = "map",
            //    CenterLatitude = 46.094602,
            //    CenterLongitude = 10.998050,
            //    Zoom = 4,
            //    TileUrlTemplate = "http://#= subdomain #.tile.openstreetmap.org/#= zoom #/#= x #/#= y #.png",
            //    TileSubdomains = new string[] { "a", "b", "c" },
            //    TileAttribution = "&copy; <a href='http://osm.org/copyright'>OpenStreetMap contributors</a>",
            //    Markers = listMarker
            //};

            //return View(map);

            return View();
        }

        public ActionResult ClusterMap()
        {
            ViewBag.Title = "Interactive Map Page";

            return View();
        }

        public ActionResult GetProductionUnitMeasures(int id)
        {
            var db = new ApplicationDbContext();

            var currentProductionUnit = db.ProductionUnits.Where(p => p.picturePath != null && p.lastMeasureReceived != null).ToList()[id];                                         

            var waterTempSensorValueSet = SensorValueManager.GetSensorValueSet(currentProductionUnit.Id, SensorTypeEnum.waterTemperature, db);

            return Json(new { 
                             CurrentWaterTempValue = waterTempSensorValueSet.CurrentValue,
                             CurrentWaterTempCaptureTime = waterTempSensorValueSet.CurrentCaptureTime,
                             AverageHourWaterTempValue = waterTempSensorValueSet.AverageHourValue,
                             AverageDayWaterTempValue = waterTempSensorValueSet.AverageDayValue,
                             LastDayWaterTempCaptureTime = waterTempSensorValueSet.LastDayCaptureTime,
            }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetProductionUnitIndex(string SelectedProductionUnitCoord)
        {
            var db = new ApplicationDbContext();

            NumberStyles style;
            CultureInfo culture;

            style = NumberStyles.AllowDecimalPoint;
            culture = CultureInfo.CreateSpecificCulture("en-US");

            var strLat = SelectedProductionUnitCoord.Split('|')[0];
            var strLong = SelectedProductionUnitCoord.Split('|')[1];

            double latitude = 0;
            double longitude = 0;

            if (double.TryParse(strLat, style, culture, out latitude) && double.TryParse(strLong, style, culture, out longitude))
            {
                var currentProductionUnit = db.ProductionUnits.Where(p => p.picturePath != null && p.lastMeasureReceived != null).ToList();

                var currentProductionUnitIndex = currentProductionUnit.FindIndex(p => p.locationLatitude == latitude &&
                                                                                 p.locationLongitude == longitude);

                return Json(new
                {
                    CurrentIndex = currentProductionUnitIndex
                }, JsonRequestBehavior.AllowGet);
            }
            else
                return null;
        }

        public ActionResult GetProductionUnitDetailList()
        {
            var db = new ApplicationDbContext();

            var prodUnitListCount = db.ProductionUnits.Where(p => p.picturePath != null && p.lastMeasureReceived != null).Count();

            if (prodUnitListCount == 0)
                return null;

            var currentProductionUnitList = db.ProductionUnits.Where(p => p.picturePath != null && p.lastMeasureReceived != null)
                                         .Include(p => p.owner.preferedMoment)
                                         .Include(p => p.productionUnitType)
                                         .Include(p => p.productionUnitStatus)
                                         .Where(p => p.picturePath != null).ToList();

            var lst = new List<object>();

            currentProductionUnitList.ForEach(p =>
            {
                var options = db.OptionLists.Include(o => o.productionUnit)
                .Include(o => o.option)
                .Where(o => o.productionUnit.Id == p.Id)
                .Select(o => o.option);

                var optionList = string.Empty;

                if (options.Count() > 0)
                {
                    options.ToList().ForEach(o => { optionList += o.name + " / "; });
                }

                if (p.owner.preferedMoment != null && p.owner.phoneNumber != String.Empty && p.owner.contactMail != String.Empty)
                {
                    lst.Add(new
                    {
                        PioneerCitizenName = p.owner.pioneerCitizenName,
                        PioneerCitizenNumber = p.owner.pioneerCitizenNumber,
                        ProductionUnitVersion = p.version,
                        ProductionUnitStartDate = p.startDate,
                        ProductionUnitType = p.productionUnitType.name,
                        ProductionUnitStatus = p.productionUnitStatus.name,
                        PhoneNumber = p.owner.phoneNumber,
                        ContactMail = p.owner.contactMail,
                        PicturePath = p.picturePath,
                        PreferedMoment = p.owner.preferedMoment.name,
                        Location = p.owner.location,

                        LocationLatitude = p.locationLatitude,
                        LocationLongitude = p.locationLongitude,

                        ProductionUnitOptions = optionList,
                    });

                }
                else
                {
                    lst.Add(new
                    {
                        PioneerCitizenName = p.owner.pioneerCitizenName,
                        PioneerCitizenNumber = p.owner.pioneerCitizenNumber,
                        ProductionUnitVersion = p.version,
                        ProductionUnitStartDate = p.startDate,
                        ProductionUnitType = p.productionUnitType.name,
                        ProductionUnitStatus = p.productionUnitStatus.name,
                        PicturePath = p.picturePath,

                        LocationLatitude = p.locationLatitude,
                        LocationLongitude = p.locationLongitude,

                        ProductionUnitOptions = optionList,
                    }
                    );
                }
            });

            return Json(lst, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetNetworkStats()
        {
            ApplicationDbContext db = new ApplicationDbContext();
            MeasureService measureService = new MeasureService(db);

            var rslt = db.ProductionUnits.Include("productionUnitType")
                                         .Where(p => p.productionUnitType.Id <= 5);

            var productionUnitNumber = rslt.Count();

            var totalBalcony = rslt.Where(p => p.productionUnitType.Id == 1).Count();
            var totalCity = rslt.Where(p => p.productionUnitType.Id == 2).Count();
            var totalFamily14 = rslt.Where(p => p.productionUnitType.Id == 3).Count();
            var totalFamily22 = rslt.Where(p => p.productionUnitType.Id == 4).Count();
            var totalFarm = rslt.Where(p => p.productionUnitType.Id == 5).Count();

            var totalMonthlyProduction = totalBalcony * 4 + totalCity * 7 + totalFamily14 * 10 + totalFamily22 * 15 + totalFarm * 25;
            var totalMonthlySparedCO2 = Math.Round(totalMonthlyProduction * 0.3,0);

            return Json(new
            {
                ProductionUnitNumber = productionUnitNumber,
                TotalMonthlyProduction = totalMonthlyProduction,
                TotalMonthlySparedCO2 = totalMonthlySparedCO2,
            }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult ProductionUnitStatus_Read([DataSourceRequest] DataSourceRequest request)
        {
            var db = new ApplicationDbContext();

            var rslt = db.ProductionUnits.Include("productionUnitStatus").ToList();

            var waitConfCount = rslt.Where(p => p.productionUnitStatus.Id == 1).Count();
            var setupPlannedCount = rslt.Where(p => p.productionUnitStatus.Id == 2).Count();
            var upRunningCount = rslt.Where(p => p.productionUnitStatus.Id == 3).Count();
            var onMaintenanceCount = rslt.Where(p => p.productionUnitStatus.Id == 4).Count();
            var stoppedCount = rslt.Where(p => p.productionUnitStatus.Id == 5).Count();
            var offineCount = rslt.Where(p => p.productionUnitStatus.Id == 6).Count();

            var statusList = new List<PieChartViewModel>();

            statusList.Add(new PieChartViewModel() { Category = "[[[Wait Confirm.]]]", Value = waitConfCount, Color = "#9de219" });
            statusList.Add(new PieChartViewModel() { Category = "[[[Setup Planned]]]", Value = setupPlannedCount, Color = "#90cc38" });
            statusList.Add(new PieChartViewModel() { Category = "[[[Up & Running]]]", Value = upRunningCount, Color = "#068c35" });
            statusList.Add(new PieChartViewModel() { Category = "[[[On Maintenance]]]", Value = onMaintenanceCount, Color = "#006634" });
            statusList.Add(new PieChartViewModel() { Category = "[[[Stopped]]]", Value = stoppedCount, Color = "#004d38" });
            statusList.Add(new PieChartViewModel() { Category = "[[[Offline]]]", Value = offineCount, Color = "#003F38" });

            return Json(statusList);
        }
    }
}
