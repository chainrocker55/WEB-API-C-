﻿using FLEX.API.Common;
using FLEX.API.Context;
using FLEX.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace FLEX.API.Services
{
    public interface IFlexDataSvc
    {
        UserInfo UserLogin(string UserId, string Password);
        List<NavData> GetMenu(string UserCd);
        List<TZ_MESSAGE_MS> GetMessage(string LangCd);
        List<TZ_SCREEN_DETAIL_LANG_MS> GetScreenDetail(string LangCd);
        TZ_USER_MS GetUserProfile(string UserCd);
        List<Notify> GetNotify(string UserCd);

        #region Combo
        List<TZ_LANG_MS> GetLanguage();
        List<TZ_USER_GROUP_MS> GetUserGroup();
        List<TZ_MENU_SET_MS> GetMenuSet();
        List<TBM_DIVISION> GetDivision();
        List<TBM_POSITION> GetPosition();
        #endregion
    }

    public class FlexDataSvc : IFlexDataSvc
    {
        private readonly AppSettings _appSettings;
        private readonly FLEXContext ct;

        public FlexDataSvc(IOptions<AppSettings> appSettings, FLEXContext context)
        {
            _appSettings = appSettings.Value;
            ct = context;
        }
        public UserInfo UserLogin(string UserCd, string Password)
        {
            var user = ct.UserLogin.FromSqlRaw(
                "SELECT USER_ACCOUNT as [USER_CD], FULL_NAME, EMAILADDR, GROUP_CD, LANG_CD, null as [TOKEN] " +
                "FROM TZ_USER_MS WHERE USER_ACCOUNT = {0} AND PASS = {1}"
                , UserCd, Password).SingleOrDefault();

            // return null if user not found
            if (user == null)
                return null;

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim("USER_CD", user.USER_CD),
                    new Claim("USER_FULLNAME", user.FULL_NAME),
                    new Claim("EMAILADDR", user.EMAILADDR??""),
                    new Claim("GROUP_CD", user.GROUP_CD),
                    new Claim("LANG_CD", user.LANG_CD),
                }),
                Expires = DateTime.UtcNow.AddHours(8),
                //Expires = DateTime.UtcNow.AddSeconds(60),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            user.TOKEN = tokenHandler.WriteToken(token);

            return user;
        }
        public List<NavData> GetMenu(string UserCd)
        {
            var menuList = ct.sp_SYS_GetMenu.FromSqlRaw("sp_SYS_GetMenu {0}", UserCd).ToList();

            List<NavData> menus = menuList?.GroupBy(g => new
            {
                MENU_SUB_CD = g.MENU_SUB_CD,
                MENU_SUB_DESC = g.MENU_SUB_DESC,
                MENU_SUB_URL = g.MENU_SUB_URL,
                MENU_SUB_ICON = g.MENU_SUB_ICON,
            }).Select(x => new NavData
            {
                ScreenCd = x.Key.MENU_SUB_CD,
                name = x.Key.MENU_SUB_DESC,
                url = $"/{x.Key.MENU_SUB_URL}",
                icon = x.Key.MENU_SUB_ICON,
                children = x.Select(y => new NavData()
                {
                    ScreenCd = y.SCREEN_CD,
                    name = y.SCREEN_DESC,
                    url = $"/{y.MENU_SUB_URL}/{y.SCREEN_URL}",
                    icon = y.SCREEN_ICON,
                }).ToList()
            }).ToList();

            menus?.Insert(0, new NavData()
            {
                name = "Dashboard",
                url = "/dashboard",
                icon = "icon-speedometer",
            });

            return menus;
        }
        public List<TZ_MESSAGE_MS> GetMessage(string LangCd)
        {
            if (string.IsNullOrEmpty(LangCd))
            {
                return ct.TZ_MESSAGE_MS.ToList();
            }
            else
            {
                return ct.TZ_MESSAGE_MS.Where(x => x.LANG_CD == LangCd).ToList();
            }
        }
        public List<TZ_SCREEN_DETAIL_LANG_MS> GetScreenDetail(string LangCd)
        {
            if (string.IsNullOrEmpty(LangCd))
            {
                return ct.TZ_SCREEN_DETAIL_LANG_MS.ToList();
            }
            else
            {
                return ct.TZ_SCREEN_DETAIL_LANG_MS.Where(x => x.LANG_CD == LangCd).ToList();
            }
        }
        public TZ_USER_MS GetUserProfile(string UserCd)
        {
            return ct.TZ_USER_MS.SingleOrDefault(x => x.USER_ACCOUNT == UserCd);
        }
        public List<Notify> GetNotify(string UserCd)
        {
            return ct.sp_Common_GetNotify.FromSqlRaw("sp_Common_GetNotify {0}", UserCd).ToList();
        }

        #region Combo
        public List<TZ_LANG_MS> GetLanguage()
        {
            return ct.TZ_LANG_MS.ToList();
        }
        public List<TZ_USER_GROUP_MS> GetUserGroup()
        {
            return ct.TZ_USER_GROUP_MS.ToList();
        }
        public List<TZ_MENU_SET_MS> GetMenuSet()
        {
            return ct.TZ_MENU_SET_MS.ToList();
        }
        public List<TBM_DIVISION> GetDivision()
        {
            return ct.TBM_DIVISION.OrderBy(x => x.CODE).ToList();
        }
        public List<TBM_POSITION> GetPosition()
        {
            return ct.TBM_POSITION.OrderBy(x => x.POSITIONCODE).ToList();
        }
        #endregion
    }
}
