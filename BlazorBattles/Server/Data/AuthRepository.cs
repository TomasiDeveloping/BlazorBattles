﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BlazorBattles.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BlazorBattles.Server.Data
{
    public class AuthRepository : IAuthRepository
    {
        private readonly DataContext _context;
        private readonly IConfiguration _configuration;

        public AuthRepository(DataContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        public async Task<ServiceResponse<int>> Register(User user, string password, int startUnitId)
        {
            if (await UserExists(user.Email))
            {
                return new ServiceResponse<int>()
                {
                    Success = false,
                    Message = "User already exists"
                };
            }
            
            CreatePasswordHash(password, out byte[] passwordHash, out byte[] passwordSalt);

            user.PasswordHash = passwordHash;
            user.PasswordSalt = passwordSalt;
            
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            await AddStartingUnit(user, startUnitId);

            return new ServiceResponse<int>()
            {
                Data = user.Id,
                Message = "Registration successful!"
            };
        }

        private async Task AddStartingUnit(User user, int startUnitId)
        {
            var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == startUnitId);
            await _context.UserUnits.AddAsync(new UserUnit()
            {
                UnitId = unit.Id,
                UserId = user.Id,
                HitPoints = unit.HitPoints
            });
            await _context.SaveChangesAsync();
        }

        public async Task<ServiceResponse<string>> Login(string email, string password)
        {
            var response = new ServiceResponse<string>();

            User user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower().Equals(email.ToLower()));
            if (user == null)
            {
                response.Success = false;
                response.Message = "User not found";
            }
            else if (!VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt))
            {
                response.Success = false;
                response.Message = "Wrong password";
            }
            else
            {
                response.Data = CreateToken(user);
            }

            return response;
            
        }

        public async Task<bool> UserExists(string email)
        {
            if (await _context.Users.AnyAsync(x => x.Email.ToLower() == email.ToLower()))
            {
                return true;
            }

            return false;
        }

        private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA512();
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        private bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA512(passwordSalt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

            return !computedHash.Where((t, i) => t != passwordHash[i]).Any();
        }

        private string CreateToken(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetSection("AppSettings:Token").Value));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return jwt;
        }
    }
}