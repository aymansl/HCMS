using HCMS4.Data;
using HCMS4.Models;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Controllers
{

    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }


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
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }

                
                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");

                   
                    var roles = await _userManager.GetRolesAsync(user);

                    if (returnUrl != null)
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

                    return RedirectToAction("Index", "Home");
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }
            }

            return View(model);
        }

        
        [HttpGet]
        public IActionResult Register()
        {
            var model = new RegisterViewModel();
            return View(model); 
        }


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

                    var patient = new Patient
                    {
                        UserId = user.Id,
                        User = user,
                        DateOfBirth = model.DateOfBirth,
                        Address = null,
                        EmergencyContact = null,
                        ChronicConditions = model.ChronicConditions, 
                    };
                    _context.Patients.Add(patient);
                    await _context.SaveChangesAsync();

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

            var profile = new ProfileViewModel
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.ToList(),
                HasDoctorProfile = await _context.Doctors.AnyAsync(d => d.UserId == user.Id),
                HasPatientProfile = await _context.Patients.AnyAsync(p => p.UserId == user.Id)
            };

            
            if (roles.Contains("Doctor"))
            {
                var doctor = await _context.Doctors
                    .FirstOrDefaultAsync(d => d.UserId == user.Id);

                if (doctor != null)
                {
                    profile.Specialization = doctor.SpecializationName;
                    profile.Qualifications = doctor.Qualifications;
                    profile.ContactInfo = doctor.ContactInfo;
                }
            }

            if (roles.Contains("Patient"))
            {
                var patient = await _context.Patients
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

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
                var doctor = await _context.Doctors
                    .Include(d => d.Specialization)
                    .FirstOrDefaultAsync(d => d.UserId == user.Id);

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
                var patient = await _context.Patients
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

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
                            var doctor = await _context.Doctors
                                .FirstOrDefaultAsync(d => d.UserId == user.Id);

                            if (doctor != null)
                            {
                                doctor.SpecializationId = model.SpecializationId;
                                doctor.Qualifications = model.Qualifications;
                                doctor.ContactInfo = model.ContactInfo;

                                _context.Doctors.Update(doctor);
                            }
                        }

                        
                        if (User.IsInRole("Patient"))
                        {
                            var patient = await _context.Patients
                                .FirstOrDefaultAsync(p => p.UserId == user.Id);

                            if (patient != null)
                            {
                                patient.DateOfBirth = model.DateOfBirth;
                                patient.Address = model.Address;
                                patient.EmergencyContact = model.EmergencyContact;

                                _context.Patients.Update(patient);
                            }
                        }

                        await _context.SaveChangesAsync();

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

            return View(model);
        }


        


    }
}
