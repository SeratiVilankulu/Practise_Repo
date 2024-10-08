using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using api.Data;
using api.Dtos.Account;
using api.Dtos.Password;
using api.Interfaces;
using api.Models;
using api.Service;
using Azure.Core;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MimeKit.Text;

namespace api.Controllers
{
  [Route("api/account")]
  [ApiController]

  public class AccountController : ControllerBase
  {
    private readonly ApplicationDBContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly SignInManager<AppUser> _signinManager;
    private readonly EmailService _emailservice;
    public AccountController(ApplicationDBContext context, UserManager<AppUser> userManager,
    ITokenService tokenService, SignInManager<AppUser> signInManager, EmailService emailservice)
    {
      _context = context;
      _userManager = userManager;
      _tokenService = tokenService;
      _signinManager = signInManager;
      _emailservice = emailservice;
    }

    //Post: Account/Register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {

      if (!ModelState.IsValid)
        return BadRequest(ModelState);

      // Create a new AppUser object
      var appUser = new AppUser
      {
        UserName = registerDto.UserName,
        Email = registerDto.Email,
      };

      var createdUser = await _userManager.CreateAsync(appUser, registerDto.Password);

      if (createdUser.Succeeded)
      {
        // Generate a token for the user
        var userToken = await _userManager.GenerateEmailConfirmationTokenAsync(appUser);
        var encodedUserToken = HttpUtility.UrlEncode(userToken);
        if (userToken != null)
        {
          var emailConfirmationLink = $"http://localhost:5085/api/account/emailconfirmation?token={encodedUserToken}&email={appUser.Email}";

          var recipient = registerDto.Email.ToLower();
          var subject = "Email Verification";
          var header = $"Dear {appUser.UserName}";
          var thanks = $"Thank you for joining our picture sharing community!";
          var message = $"Please confirm your account by clicking this link: <a href='{emailConfirmationLink}'>Confirm Email</a>";
          var ending = $"Kind Regards";
          var sign = $"Image Gallery App";

          // Send the email
          await _emailservice.SendEmailAsync(recipient, subject, header, thanks, message, ending, sign);

          return Ok("Email was succefully sent, please check emails for confirmation link");
        }
        else
        {
          throw new Exception("Unable to generate email confirmation token");
        }

      }
      return BadRequest("Error Occured. Unable to send email");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
      if (!ModelState.IsValid)
        return BadRequest(ModelState);

      var user = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == loginDto.Username.ToLower());

      if (user == null)
        return Unauthorized("Username not found");


      var result = await _signinManager.CheckPasswordSignInAsync(user, loginDto.Password, lockoutOnFailure: true);

      if (result.Succeeded) // Checks if the sign-in was successful and shows user details
      {
        return Ok(new NewUserDto
        {
          AppUserId = user.Id,
          UserName = user.UserName,
          Email = user.Email,
          VerificationToken = _tokenService.CreateToken(user)
        });
      }
      else if (result.IsLockedOut) // If users account has been locked out
      {
        return BadRequest("Account has been blocked. Please try after 5 minutes.");
      }

      if (result.IsNotAllowed) // Checks if users email has been verified, if not this message is displayed
      {
        return BadRequest("Email has not been verified, please check email and verify account.");
      }
      else
      {
        var accessFailedCount = await _userManager.GetAccessFailedCountAsync(user);
        var maxFailedAccessAttempts = _userManager.Options.Lockout.MaxFailedAccessAttempts;
        var attemptsLeft = maxFailedAccessAttempts - accessFailedCount;

        return Unauthorized(new
        {
          message = $"Invalid username or password. You have {attemptsLeft} attempts left.",

        });
      }
    }

    //Registration Verification (Once user clicks on link)
    [HttpGet("emailconfirmation")]
    public async Task<IActionResult> EmailConfirmation([FromQuery] string email, [FromQuery] string token)
    {
      // Fetch the user using email
      var user = await _userManager.FindByEmailAsync(email);
      if (user == null)
        return BadRequest("Invalid Email confirmation Request");

      // Validate if the email confirmation logic token matches specified user
      var confirmResult = await _userManager.ConfirmEmailAsync(user, token);
      if (!confirmResult.Succeeded)
        return BadRequest("Invalid Email confirmation Request");

      // Mark the email as verified
      user.VerifiedDate = DateTime.Now;
      await _context.SaveChangesAsync();

      // Redirect to the login page after successful verification
      return Redirect("http://localhost:3000/email-success");
    }

    // Forgot password
    [HttpPost("forgotpassword")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
    {
      if (!ModelState.IsValid)
        return BadRequest("Invalid email");

      try
      {
        // Extract user from the database
        var user = await _userManager.FindByEmailAsync(forgotPasswordDto.Email);
        if (user == null)
          return BadRequest("Email not found");

        // Generate a token for the user
        var userResetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedUserToken = HttpUtility.UrlEncode(userResetToken);
        if (userResetToken != null)
        {
          var emailResetLink = $"http://localhost:3000/reset-password?token={encodedUserToken}&email={forgotPasswordDto.Email}"; // Link which directs user to Reset password page

          var recipient = forgotPasswordDto.Email.ToLower();
          var subject = "Password Reset";
          var header = $"Dear User";
          var thanks = $"We are sorry to hear that you forgot your password!";
          var message = $"Please click this link to reset password: <a href='{emailResetLink}'>Reset Password Link</a>";
          var ending = $"Kind Regards";
          var sign = $"Image Gallery App";
          try
          {
            // Send the email
            await _emailservice.SendEmailAsync(recipient, subject, header, thanks, message, ending, sign);
          }
          catch (Exception)
          {
            return BadRequest("Unfortunately this email cannot be sent");
          }


          return Ok("Reset password email was successfully sent!");
        }
      }
      catch (Exception)
      {
        return BadRequest("Something went wrong with generating reset token, please try again.");

      }
      return BadRequest("Error Occured. Unable to send email");
    }

    //Resetting the users password
    [HttpPost("resetpassword")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
      if (!ModelState.IsValid)
        return BadRequest("Invalid request.");

      // Extract user from the database
      var user = await _userManager.FindByEmailAsync(resetPasswordDto.Email);
      if (user == null)
        return BadRequest("Invalid Email");

      // Validate the token (check if the token is expired or invalid)
      var isTokenValid = await _userManager.VerifyUserTokenAsync(
          user,
          _userManager.Options.Tokens.PasswordResetTokenProvider,
          "ResetPassword",
          resetPasswordDto.Token
      );

      if (!isTokenValid)
      {
        return BadRequest("The reset token is invalid or has expired. Please request a new password reset.");
      }

      // Check if the new password is the same as the old password
      var currentPasswordVerification = _userManager.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, resetPasswordDto.NewPassword);
      if (currentPasswordVerification == PasswordVerificationResult.Success)
      {
        return BadRequest("The new password cannot be the same as the old password.");
      }

      // Proceed to reset the password if the token is valid
      var resetResult = await _userManager.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.NewPassword);

      if (!resetResult.Succeeded)
      {
        var errors = resetResult.Errors.Select(e => e.Description);
        return BadRequest(new { message = "Password reset process has failed", errors });
      }

      // Update the user's password change date (optional)
      user.PasswordChangeDate = DateTime.UtcNow;
      await _userManager.UpdateAsync(user);

      return Ok("Password has been reset successfully.");
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
    {
      if (!ModelState.IsValid)
        return BadRequest("Invalid input.");

      // Get the logged-in user's email from the claims
      var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
      if (string.IsNullOrEmpty(email))
        return BadRequest("Invalid user.");

      // Fetch the user from the database
      var user = await _userManager.FindByEmailAsync(email);
      if (user == null)
        return BadRequest("User not found.");

      // Check if the new password is the same as the current one
      var currentPasswordVerification = _userManager.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, changePasswordDto.NewPassword);
      if (currentPasswordVerification == PasswordVerificationResult.Success)
      {
        return BadRequest("The new password cannot be the same as the old password.");
      }

      // Change password
      var result = await _userManager.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);
      if (!result.Succeeded)
      {
        var errors = result.Errors.Select(e => e.Description);
        return BadRequest($"Failed to change password: {string.Join(", ", errors)}");
      }

      return Ok("Password changed successfully.");
    }


    // Logout
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
      try
      {
        await _signinManager.SignOutAsync();
        return Ok("Logged out successfully");
      }
      catch (Exception ex)
      {
        return BadRequest("Something went wrong, please try again. " + ex.Message);
      }
    }
  }
}