using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Identity.Authorization;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Identity.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Shared.Identity.Controllers
{

    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IPatientService _patientService;
        private readonly IStaffService _staffService;
        private readonly IEnumerable<IAccountStatusCheck> _accountStatusChecks;
        private readonly ILogger<AccountController> _logger;

        // Patient and doctor details belong to the Records and Staff modules; the account pages use their services.
        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IPatientService patientService,
            IStaffService staffService,
            IEnumerable<IAccountStatusCheck> accountStatusChecks,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _patientService = patientService;
            _staffService = staffService;
            _accountStatusChecks = accountStatusChecks;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }


        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    return View(model);
                }

                
                // Verify the password (and lockout) first, then refuse suspended accounts before any
                // session cookie is issued.
                var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

                if (result.Succeeded && await IsSuspendedAsync(user.Id))
                {
                    _logger.LogWarning("Sign-in refused for suspended account {UserId}.", user.Id);
                    ModelState.AddModelError(string.Empty, AccountStatusMessages.Suspended);
                    return View(model);
                }

                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, model.RememberMe);
                    _logger.LogInformation("User logged in.");

                   
                    var roles = await _userManager.GetRolesAsync(user);

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }
                    else if (roles.Contains("Admin"))
                    {
                        return RedirectToAction("Dashboard", "Admin");
                    }
                    else if (roles.Contains("Doctor"))
                    {
                        return RedirectToAction("Dashboard", "Doctor");
                    }
                    else if (roles.Contains("Patient"))
                    {
                        return RedirectToAction("Dashboard", "Patient");
                    }
                    else if (roles.Contains("Pharmacist"))
                    {
                        return RedirectToAction("Dashboard", "Pharmacist");
                    }
                    else if (roles.Contains(Roles.LabTechnician))
                    {
                        return RedirectToAction("Dashboard", "Lab");
                    }

                    return RedirectToAction("Index", "Home");
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    ModelState.AddModelError(string.Empty,
                        "Your account is temporarily locked after too many failed sign-in attempts. Try again in 5 minutes.");
                    return View(model);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    return View(model);
                }
            }

            return View(model);
        }

        private async Task<bool> IsSuspendedAsync(string userId)
        {
            foreach (var check in _accountStatusChecks)
            {
                if (await check.IsSuspendedAsync(userId))
                {
                    return true;
                }
            }

            return false;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register()
        {
            var model = new RegisterViewModel();
            return View(model); 
        }


        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    
                    await _userManager.AddToRoleAsync(user, "Patient");

                    await _patientService.CreateForUserAsync(user.Id, model.DateOfBirth, model.ChronicConditions);

                    await _signInManager.SignInAsync(user, isPersistent: false);

                    _logger.LogInformation("User created a new account with password.");

                    return RedirectToAction("Dashboard", "Patient");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description); 
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userName = User.Identity?.Name;
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User {UserName} logged out.", userName);

            if (HttpContext.Session != null)
            {
                HttpContext.Session.Clear();
            }

            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var doctor = await _staffService.GetDoctorByUserIdAsync(user.Id);
            var patient = await _patientService.GetByUserIdAsync(user.Id);

            var profile = new ProfileViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.ToList(),
                HasDoctorProfile = doctor != null,
                HasPatientProfile = patient != null
            };

            if (roles.Contains("Doctor"))
            {
                if (doctor != null)
                {
                    profile.Specialization = doctor.SpecializationName;
                    profile.Qualifications = doctor.Qualifications;
                    profile.ContactInfo = doctor.ContactInfo;
                }
            }

            if (roles.Contains("Patient"))
            {
                if (patient != null)
                {
                    profile.DateOfBirth = patient.DateOfBirth;
                    profile.Address = patient.Address;
                    profile.EmergencyContact = patient.EmergencyContact;
                }
            }



            return View(profile);
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var editModel = new EditProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber
            };

           
            if (User.IsInRole("Doctor"))
            {
                var doctor = await _staffService.GetDoctorByUserIdAsync(user.Id);

                if (doctor != null)
                {
                    editModel.SpecializationId = doctor.SpecializationId;
                    editModel.Qualifications = doctor.Qualifications;
                    editModel.ContactInfo = doctor.ContactInfo;
                    editModel.SpecializationName = doctor.Specialization?.Name;
                }
            }

            if (User.IsInRole("Patient"))
            {
                var patient = await _patientService.GetByUserIdAsync(user.Id);

                if (patient != null)
                {
                    editModel.DateOfBirth = patient.DateOfBirth;
                    editModel.Address = patient.Address;
                    editModel.EmergencyContact = patient.EmergencyContact;
                }
            }

            return View(editModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login");
                }

                try
                {
                    
                    user.FirstName = model.FirstName;
                    user.LastName = model.LastName;
                    user.PhoneNumber = model.PhoneNumber;

                    var result = await _userManager.UpdateAsync(user);

                    if (result.Succeeded)
                    {
                        
                        if (User.IsInRole("Doctor"))
                        {
                            await _staffService.UpdateOwnDoctorDetailsAsync(
                                user.Id, model.SpecializationId, model.Qualifications, model.ContactInfo);
                        }

                        if (User.IsInRole("Patient"))
                        {
                            await _patientService.UpdateOwnDetailsAsync(
                                user.Id, model.DateOfBirth, model.Address, model.EmergencyContact);
                        }

                        TempData["SuccessMessage"] = "Profile updated successfully!";
                        return RedirectToAction("Profile");
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating profile for user {UserId}", user.Id);
                    ModelState.AddModelError(string.Empty, "An error occurred while updating your profile.");
                }
            }

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("Login");
                }

                var result = await _userManager.ChangePasswordAsync(
                    user, model.CurrentPassword, model.NewPassword);

                if (result.Succeeded)
                {
                    await _signInManager.RefreshSignInAsync(user);
                    TempData["SuccessMessage"] = "Password changed successfully!";
                    return RedirectToAction("Profile");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // The form is part of the Profile page (there is no ChangePassword view), so go back there with the errors.
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct();
            TempData["ErrorMessage"] = string.Join(" ", errors);
            return RedirectToAction("Profile");
        }


        


    }
}
