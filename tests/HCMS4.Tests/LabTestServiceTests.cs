using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.Services;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.Tests
{
    /// <summary>UC-L06 — Manage the lab test catalogue (TC-L6-01 … TC-L6-03 and the validation rules).</summary>
    public class LabTestServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();
        private readonly StubUsage _usage = new();

        // 2026-11-19 09:00 UTC = 12:00 Damascus.
        private readonly ClinicClock _clock = new(
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero)), ClinicTime.TimeZone);

        private LabTestService Service => new(_db, _usage, _clock);

        public void Dispose() => _db.Dispose();

        private sealed class StubUsage : ILabTestUsage
        {
            public HashSet<int> UsedIds { get; } = new();
            public Task<bool> IsUsedAsync(int labTestId) => Task.FromResult(UsedIds.Contains(labTestId));
        }

        private static LabTestFormViewModel Cbc() => new()
        {
            Name = "CBC", Unit = "10^9/L", ReferenceMin = 4, ReferenceMax = 11, Price = 5000
        };

        private async Task<LabTest> AddAsync(LabTestFormViewModel model)
        {
            var result = await Service.SaveAsync(model);
            Assert.True(result.Success, result.Message);
            return result.Data;
        }

        private static List<ValidationResult> Validate(LabTestFormViewModel model)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results;
        }

        [Fact]
        public async Task TC_L6_01_AddCbc_AppearsInTheCatalogueAsActive()
        {
            var result = await Service.SaveAsync(Cbc());

            Assert.True(result.Success, result.Message);
            var test = Assert.Single(await Service.GetAllAsync());
            Assert.Equal("CBC", test.Name);
            Assert.Equal("10^9/L", test.Unit);
            Assert.Equal(4, test.ReferenceMin);
            Assert.Equal(11, test.ReferenceMax);
            Assert.Equal(5000, test.Price);
            Assert.True(test.IsActive);
            Assert.Equal(_clock.Now, test.CreatedAt);
            Assert.Equal("4 – 11", test.ReferenceRange);
            Assert.Equal(1, await Service.CountActiveAsync());
        }

        [Fact]
        public async Task Add_TrimsNameAndUnit()
        {
            var model = Cbc();
            model.Name = "  Glucose ";
            model.Unit = " mg/dL ";

            var test = await AddAsync(model);

            Assert.Equal("Glucose", test.Name);
            Assert.Equal("mg/dL", test.Unit);
        }

        [Fact]
        public async Task TC_L6_02_MinGreaterThanMax_IsRejected_AndNothingIsSaved()
        {
            var model = Cbc();
            model.ReferenceMin = 11;
            model.ReferenceMax = 4;

            var result = await Service.SaveAsync(model);

            Assert.False(result.Success);
            Assert.Equal(LabTestFormViewModel.RangeError, result.Message);
            Assert.Empty(await _db.LabTests.ToListAsync());
        }

        [Fact]
        public void TC_L6_02_FormValidation_ReportsTheRangeError()
        {
            var model = Cbc();
            model.ReferenceMin = 11;
            model.ReferenceMax = 4;

            var error = Assert.Single(Validate(model));
            Assert.Equal(LabTestFormViewModel.RangeError, error.ErrorMessage);
            Assert.Contains(nameof(LabTestFormViewModel.ReferenceMin), error.MemberNames);
        }

        [Fact]
        public void FormValidation_AcceptsAValidTest_AndAQualitativeOne()
        {
            Assert.Empty(Validate(Cbc()));
            Assert.Empty(Validate(new LabTestFormViewModel { Name = "Urine Culture", Unit = "-", Price = 0 }));
        }

        [Fact]
        public void FormValidation_RejectsANegativePrice_AndMissingFields()
        {
            var negative = Cbc();
            negative.Price = -1;
            Assert.Contains(Validate(negative), r => r.MemberNames.Contains(nameof(LabTestFormViewModel.Price)));

            var empty = Validate(new LabTestFormViewModel());
            Assert.Contains(empty, r => r.MemberNames.Contains(nameof(LabTestFormViewModel.Name)));
            Assert.Contains(empty, r => r.MemberNames.Contains(nameof(LabTestFormViewModel.Unit)));
            Assert.Contains(empty, r => r.MemberNames.Contains(nameof(LabTestFormViewModel.Price)));
        }

        [Fact]
        public async Task Add_NegativePrice_IsRejected()
        {
            var model = Cbc();
            model.Price = -10;

            var result = await Service.SaveAsync(model);

            Assert.False(result.Success);
            Assert.Empty(await _db.LabTests.ToListAsync());
        }

        [Fact]
        public async Task Add_BlankName_IsRejected()
        {
            var model = Cbc();
            model.Name = "   ";

            Assert.False((await Service.SaveAsync(model)).Success);
        }

        [Fact]
        public async Task Add_QualitativeTest_HasNoReferenceRange()
        {
            var test = await AddAsync(new LabTestFormViewModel { Name = "Urine Culture", Unit = "-", Price = 7500 });

            Assert.Null(test.ReferenceMin);
            Assert.Null(test.ReferenceMax);
            Assert.Equal("-", test.ReferenceRange);
        }

        [Fact]
        public async Task Add_DuplicateName_IgnoringCase_IsRejected()
        {
            await AddAsync(Cbc());
            var model = Cbc();
            model.Name = " cbc ";

            var result = await Service.SaveAsync(model);

            Assert.False(result.Success);
            Assert.Equal(LabTestService.DuplicateNameError, result.Message);
            Assert.Single(await _db.LabTests.ToListAsync());
        }

        [Fact]
        public async Task Edit_UpdatesTheFields_AndTheTimestamp()
        {
            var test = await AddAsync(Cbc());
            _db.ChangeTracker.Clear();

            var result = await Service.SaveAsync(new LabTestFormViewModel
            {
                Id = test.Id, Name = "CBC", Unit = "10^9/L", ReferenceMin = 4.5m, ReferenceMax = 11, Price = 6000, IsActive = true
            });

            Assert.True(result.Success, result.Message);
            var saved = await Service.GetByIdAsync(test.Id);
            Assert.Equal(6000, saved!.Price);
            Assert.Equal(4.5m, saved.ReferenceMin);
            Assert.Equal(_clock.Now, saved.UpdatedAt);
        }

        [Fact]
        public async Task Edit_KeepingItsOwnName_IsNotADuplicate()
        {
            var test = await AddAsync(Cbc());
            var model = Cbc();
            model.Id = test.Id;
            model.Name = "cbc";

            var result = await Service.SaveAsync(model);

            Assert.True(result.Success, result.Message);
            Assert.Equal("cbc", (await Service.GetByIdAsync(test.Id))!.Name);
        }

        [Fact]
        public async Task Edit_RenamingToAnotherTestsName_IsRejected()
        {
            await AddAsync(Cbc());
            var glucose = await AddAsync(new LabTestFormViewModel { Name = "Glucose", Unit = "mg/dL", Price = 2000 });

            var result = await Service.SaveAsync(new LabTestFormViewModel
            {
                Id = glucose.Id, Name = "CBC", Unit = "mg/dL", Price = 2000, IsActive = true
            });

            Assert.False(result.Success);
            Assert.Equal(LabTestService.DuplicateNameError, result.Message);
            Assert.Equal("Glucose", (await Service.GetByIdAsync(glucose.Id))!.Name);
        }

        [Fact]
        public async Task Edit_CanDeactivate_AndActiveOnlyHidesIt()
        {
            var test = await AddAsync(Cbc());
            var model = Cbc();
            model.Id = test.Id;
            model.IsActive = false;

            Assert.True((await Service.SaveAsync(model)).Success);

            Assert.Single(await Service.GetAllAsync());
            Assert.Empty(await Service.GetAllAsync(activeOnly: true));
            Assert.Equal(0, await Service.CountActiveAsync());
        }

        [Fact]
        public async Task Edit_UnknownTest_Fails()
        {
            var model = Cbc();
            model.Id = 999;

            Assert.False((await Service.SaveAsync(model)).Success);
        }

        [Fact]
        public async Task Delete_UnusedTest_RemovesIt()
        {
            var test = await AddAsync(Cbc());

            var result = await Service.DeleteAsync(test.Id);

            Assert.True(result.Success, result.Message);
            Assert.Equal(LabTestDeleteOutcome.Deleted, result.Data);
            Assert.Empty(await _db.LabTests.ToListAsync());
        }

        [Fact]
        public async Task TC_L6_03_Delete_TestUsedByPastRequests_DeactivatesIt()
        {
            var test = await AddAsync(Cbc());
            _usage.UsedIds.Add(test.Id);

            var result = await Service.DeleteAsync(test.Id);

            Assert.True(result.Success, result.Message);
            Assert.Equal(LabTestDeleteOutcome.Deactivated, result.Data);
            Assert.Contains("deactivated", result.Message);
            var saved = await Service.GetByIdAsync(test.Id);
            Assert.NotNull(saved);
            Assert.False(saved!.IsActive);
        }

        [Fact]
        public async Task Delete_UsedTestThatIsAlreadyInactive_Fails()
        {
            var test = await AddAsync(Cbc());
            _usage.UsedIds.Add(test.Id);
            await Service.DeleteAsync(test.Id);

            var result = await Service.DeleteAsync(test.Id);

            Assert.False(result.Success);
            Assert.NotNull(await Service.GetByIdAsync(test.Id));
        }

        [Fact]
        public async Task Delete_UnknownTest_Fails()
        {
            Assert.False((await Service.DeleteAsync(999)).Success);
        }

        [Fact]
        public async Task NoLabTestUsage_ReportsNoTestInUse()
        {
            Assert.False(await new NoLabTestUsage().IsUsedAsync(1));
        }

        [Fact]
        public async Task GetAll_IsOrderedByName()
        {
            await AddAsync(new LabTestFormViewModel { Name = "Glucose", Unit = "mg/dL", Price = 2000 });
            await AddAsync(Cbc());

            Assert.Equal(new[] { "CBC", "Glucose" }, (await Service.GetAllAsync()).Select(t => t.Name));
        }

        [Theory]
        [InlineData(4.0, null, "≥ 4")]
        [InlineData(null, 200.0, "≤ 200")]
        [InlineData(0.5, 1.25, "0.5 – 1.25")]
        public void ReferenceRange_ShowsOpenAndClosedRanges(double? min, double? max, string expected)
        {
            var test = new LabTest { ReferenceMin = (decimal?)min, ReferenceMax = (decimal?)max };

            Assert.Equal(expected, test.ReferenceRange);
        }
    }
}
