﻿using myfoodapp.Hub.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace myfoodapp.Hub.Services
{
    public class EventService : IDisposable
    {
        private ApplicationDbContext entities;

        public EventService(ApplicationDbContext entities)
        {
            this.entities = entities;
        }

        public IList<EventViewModel> GetAll()
        {
            IList<EventViewModel> result = new List<EventViewModel>();

            result = entities.Events.Select(ev => new EventViewModel
            {
                Id = ev.Id,
                date = ev.date,
                description = ev.description,
                isOpen = ev.isOpen,
                createdBy = ev.createdBy,
                productionUnitId = ev.productionUnit.Id,
                productionUnit = new ProductionUnitViewModel()
                {
                    Id = ev.productionUnit.Id,
                    info = ev.productionUnit.info
                },
                eventTypeId = ev.eventType.Id,
                eventType = new EventTypeViewModel()
                {
                    Id = ev.eventType.Id,
                    name = ev.eventType.name
                }

            }).ToList();

            return result;
        }

        public IList<EventViewModel> GetAll(int currentProductionId)
        {
            IList<EventViewModel> result = new List<EventViewModel>();

            result = entities.Events.Include(e => e.productionUnit).Where(ev => ev.productionUnit.Id == currentProductionId)
                                                              .Select(ev => new EventViewModel
            {
                Id = ev.Id,
                date = ev.date,
                description = ev.description,
                isOpen = ev.isOpen,
                createdBy = ev.createdBy,
                productionUnitId = ev.productionUnit.Id,
                productionUnit = new ProductionUnitViewModel()
                {
                    Id = ev.productionUnit.Id,
                    info = ev.productionUnit.info
                },
                eventTypeId = ev.eventType.Id,
                eventType = new EventTypeViewModel()
                {
                    Id = ev.eventType.Id,
                    name = ev.eventType.name
                }

            }).ToList();

            return result;
        }

        public IEnumerable<EventViewModel> Read()
        {
            return GetAll();
        }

        public EventViewModel One(Func<EventViewModel, bool> predicate)
        {
            return GetAll().FirstOrDefault(predicate);
        }

        public void Dispose()
        {
            entities.Dispose();
        }
    }
}