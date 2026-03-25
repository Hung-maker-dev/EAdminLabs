using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eAdmin.Domain.Entities;
using eAdmin.Domain.Interfaces;

namespace eAdmin.Service
{
    /// <summary>
    /// Xuất báo cáo: tình trạng thiết bị, lịch sử khiếu nại, phần mềm hết hạn.
    /// </summary>
    public class ReportService : IReportService
    {
        private readonly IUnitOfWork _uow;
        public ReportService(IUnitOfWork uow) => _uow = uow;

        /// <summary>
        /// Báo cáo tình trạng thiết bị theo phòng Lab (null = toàn bộ).
        /// </summary>
        public async Task<IEnumerable<Equipment>> GetEquipmentConditionReportAsync(int? labId = null)
        {
            if (labId.HasValue)
                return await _uow.Equipments.FindAsync(e => e.LabId == labId.Value);
            return await _uow.Equipments.GetAllAsync();
        }

        /// <summary>
        /// Báo cáo khiếu nại trong khoảng thời gian.
        /// </summary>
        public async Task<IEnumerable<Complaint>> GetComplaintReportAsync(DateTime from, DateTime to)
            => await _uow.Complaints.FindAsync(c =>
                c.CreatedAt >= from && c.CreatedAt <= to);

        /// <summary>
        /// Báo cáo phần mềm sắp/đã hết hạn (trong 60 ngày tới).
        /// </summary>
        public async Task<IEnumerable<Software>> GetSoftwareExpiryReportAsync()
        {
            var threshold = DateTime.Today.AddDays(60);
            return await _uow.Softwares.FindAsync(s =>
                s.LicenseExpiry.HasValue &&
                s.LicenseExpiry.Value <= threshold);
        }
    }
}
