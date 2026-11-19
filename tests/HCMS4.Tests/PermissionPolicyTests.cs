using System.Reflection;
using HCMS4.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Tests
{
    // S2: permission-based policies replace role strings; the role → permission map is the single source.
    public class PermissionPolicyTests
    {
        private static IEnumerable<Type> Controllers() =>
            typeof(Program).Assembly.GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        [Fact]
        public void EveryRole_IsMapped_AndEveryPermission_BelongsToARole()
        {
            Assert.Equal(Roles.All.OrderBy(r => r), RolePermissions.ByRole.Keys.OrderBy(r => r));
            Assert.All(RolePermissions.AllPermissions, p => Assert.NotEmpty(RolePermissions.RolesWith(p)));
            Assert.All(RolePermissions.ByRole.Values.SelectMany(v => v),
                p => Assert.Contains(p, RolePermissions.AllPermissions));
        }

        [Fact]
        public void EveryController_UsesAKnownPermissionPolicy_OrIsPublic()
        {
            foreach (var controller in Controllers())
            {
                var authorize = controller.GetCustomAttribute<AuthorizeAttribute>();
                if (authorize == null)
                {
                    // Home and Account: public pages, or pages that only need a signed-in user (fallback policy).
                    Assert.Contains(controller.Name, new[] { "HomeController", "AccountController" });
                    continue;
                }

                Assert.Null(authorize.Roles);
                Assert.Contains(authorize.Policy, RolePermissions.AllPermissions);
            }
        }

        [Fact]
        public void NoEndpoint_UsesRoleStrings()
        {
            var attributes = Controllers()
                .SelectMany(c => c.GetMethods().Cast<MemberInfo>().Append(c))
                .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: false));

            Assert.DoesNotContain(attributes, a => a.Roles != null);
        }

        [Theory]
        [InlineData(Roles.Admin, Permissions.Pharmacy.Oversee, true)]
        [InlineData(Roles.Pharmacist, Permissions.Pharmacy.Oversee, false)]
        [InlineData(Roles.Pharmacist, Permissions.Pharmacy.Dispense, true)]
        [InlineData(Roles.Patient, Permissions.Feedback.ViewComplaintAttachments, true)]
        [InlineData(Roles.Doctor, Permissions.Feedback.ViewComplaintAttachments, false)]
        [InlineData(Roles.LabTechnician, Permissions.Lab.ReceiveRequest, true)]
        [InlineData(Roles.Radiologist, Permissions.Lab.ReceiveRequest, false)]
        [InlineData(Roles.EmergencySupervisor, Permissions.Emergency.ManageCases, true)]
        [InlineData("Unknown", Permissions.Portal.AdminDashboard, false)]
        public void Has_FollowsTheMap(string role, string permission, bool expected)
        {
            Assert.Equal(expected, RolePermissions.Has(role, permission));
        }
    }
}
