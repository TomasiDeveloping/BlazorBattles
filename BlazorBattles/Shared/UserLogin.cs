﻿using System.ComponentModel.DataAnnotations;

namespace BlazorBattles.Shared
{
    public class UserLogin
    {
        [Required(ErrorMessage = "Please enter a Email Address.")]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
    }
}