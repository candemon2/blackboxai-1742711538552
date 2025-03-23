using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using System.Text;

namespace AIAsistani
{
    public class DailyPlanManager
    {
        private readonly string _planFilePath = "daily_plans.json";
        private List<PlanItem> _plans;

        public event EventHandler<Exception> ErrorOccurred;
        public event EventHandler<PlanItem> PlanAdded;
        public event EventHandler<PlanItem> PlanUpdated;
        public event EventHandler<string> PlanRemoved;
        public event EventHandler<PlanItem> ReminderDue;

        public class PlanItem
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public DateTime DueDate { get; set; }
            public bool IsCompleted { get; set; }
            public PlanPriority Priority { get; set; }
            public List<string> Tags { get; set; } = new List<string>();
            public DateTime CreatedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
        }

        public enum PlanPriority
        {
            Low,
            Medium,
            High,
            Urgent
        }

        public DailyPlanManager()
        {
            _plans = new List<PlanItem>();
            LoadPlans();
            StartReminderCheck();
        }

        private async void StartReminderCheck()
        {
            while (true)
            {
                try
                {
                    CheckReminders();
                    await Task.Delay(TimeSpan.FromMinutes(1)); // Her dakika kontrol et
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }

        private void CheckReminders()
        {
            var now = DateTime.Now;
            var duePlans = _plans.Where(p => 
                !p.IsCompleted && 
                p.DueDate <= now && 
                p.DueDate > now.AddMinutes(-1)
            ).ToList();

            foreach (var plan in duePlans)
            {
                ReminderDue?.Invoke(this, plan);
            }
        }

        public async Task<PlanItem> AddPlanAsync(string title, string description, DateTime dueDate, 
            PlanPriority priority = PlanPriority.Medium, List<string> tags = null)
        {
            try
            {
                var plan = new PlanItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Description = description,
                    DueDate = dueDate,
                    Priority = priority,
                    Tags = tags ?? new List<string>(),
                    CreatedAt = DateTime.Now,
                    IsCompleted = false
                };

                _plans.Add(plan);
                await SavePlansAsync();

                PlanAdded?.Invoke(this, plan);
                return plan;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw new Exception("Plan eklenirken bir hata oluştu.", ex);
            }
        }

        public async Task<bool> UpdatePlanAsync(string id, string title = null, string description = null, 
            DateTime? dueDate = null, PlanPriority? priority = null, List<string> tags = null)
        {
            try
            {
                var plan = _plans.FirstOrDefault(p => p.Id == id);
                if (plan == null)
                    return false;

                if (title != null) plan.Title = title;
                if (description != null) plan.Description = description;
                if (dueDate.HasValue) plan.DueDate = dueDate.Value;
                if (priority.HasValue) plan.Priority = priority.Value;
                if (tags != null) plan.Tags = tags;

                await SavePlansAsync();
                PlanUpdated?.Invoke(this, plan);
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return false;
            }
        }

        public async Task<bool> CompletePlanAsync(string id)
        {
            try
            {
                var plan = _plans.FirstOrDefault(p => p.Id == id);
                if (plan == null)
                    return false;

                plan.IsCompleted = true;
                plan.CompletedAt = DateTime.Now;

                await SavePlansAsync();
                PlanUpdated?.Invoke(this, plan);
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return false;
            }
        }

        public async Task<bool> RemovePlanAsync(string id)
        {
            try
            {
                var plan = _plans.FirstOrDefault(p => p.Id == id);
                if (plan == null)
                    return false;

                _plans.Remove(plan);
                await SavePlansAsync();

                PlanRemoved?.Invoke(this, id);
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return false;
            }
        }

        public List<PlanItem> GetAllPlans()
        {
            return _plans.OrderBy(p => p.DueDate).ToList();
        }

        public List<PlanItem> GetTodaysPlans()
        {
            var today = DateTime.Today;
            return _plans
                .Where(p => p.DueDate.Date == today && !p.IsCompleted)
                .OrderBy(p => p.DueDate)
                .ToList();
        }

        public List<PlanItem> GetUpcomingPlans(int days = 7)
        {
            var endDate = DateTime.Today.AddDays(days);
            return _plans
                .Where(p => p.DueDate.Date <= endDate && !p.IsCompleted)
                .OrderBy(p => p.DueDate)
                .ToList();
        }

        public List<PlanItem> SearchPlans(string keyword)
        {
            keyword = keyword.ToLower();
            return _plans
                .Where(p => 
                    p.Title.ToLower().Contains(keyword) || 
                    p.Description.ToLower().Contains(keyword) ||
                    p.Tags.Any(t => t.ToLower().Contains(keyword)))
                .OrderBy(p => p.DueDate)
                .ToList();
        }

        private void LoadPlans()
        {
            try
            {
                if (File.Exists(_planFilePath))
                {
                    string json = File.ReadAllText(_planFilePath);
                    _plans = JsonConvert.DeserializeObject<List<PlanItem>>(json) ?? new List<PlanItem>();
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                _plans = new List<PlanItem>();
            }
        }

        private async Task SavePlansAsync()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_plans, Formatting.Indented);
                await File.WriteAllTextAsync(_planFilePath, json);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw new Exception("Planlar kaydedilirken bir hata oluştu.", ex);
            }
        }

        public string GetDailyPlanSummary()
        {
            var todaysPlans = GetTodaysPlans();
            if (!todaysPlans.Any())
                return "Bugün için planlanmış bir göreviniz bulunmuyor, Patron.";

            var summary = new StringBuilder();
            summary.AppendLine("Bugünkü planlarınız:");

            foreach (var plan in todaysPlans)
            {
                string priority = plan.Priority switch
                {
                    PlanPriority.Urgent => "⚠️ ACİL",
                    PlanPriority.High => "❗ Yüksek",
                    PlanPriority.Medium => "📌 Orta",
                    _ => "📝 Düşük"
                };

                summary.AppendLine($"{priority} - {plan.DueDate.ToString("HH:mm")} - {plan.Title}");
                if (!string.IsNullOrEmpty(plan.Description))
                    summary.AppendLine($"   {plan.Description}");
            }

            return summary.ToString();
        }
    }
}