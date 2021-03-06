﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Web.Core;
using CYQ.Data;
using CYQ.Data.Table;
using CYQ.Data.Tool;

namespace Web.Logic.Sys
{
    /// <summary>
    /// 注：本类由框架组提供，其它人不得修改。
    /// 需要扩展定义，新建一个文件，定义同名的partial类即可。
    /// </summary>
    public partial class SysLogic : LogicBase
    {
        public SysLogic(IBase custom) : base(custom) { }

        #region 用户表相关操作
        /// <summary>
        /// 更新用户by CYQ
        /// </summary>
        /// <param name="objName"></param>
        public string UpdateUser()
        {
            bool result = false;
            string pwd = Query<string>("Password");
            string userID = Query<string>("UserID");
            using (MAction action = new MAction(TableNames.Sys_User))
            {
                action.BeginTransation();
                if (!string.IsNullOrEmpty(pwd))
                {
                    action.Set(Sys_User.Password, EncrpytHelper.Encrypt(pwd));//加密
                }
                result = action.Update(userID,true);
                if (result)
                {
                    action.ResetTable(TableNames.Sys_UserInfo);
                    if (action.Exists(userID))
                    {
                        if (action.Data.Count > 1)//有自定义列
                        {
                            result = action.Update(userID,true);
                        }
                    }
                    else
                    {
                        action.Set(Sys_UserInfo.UserInfoID, userID);
                        action.AllowInsertID = true;
                        result = action.Insert(true);
                    }
                }
                if (!result)
                {
                    action.RollBack();
                }
                action.EndTransation();

            }
            return JsonHelper.OutResult(result, result ? "更新成功!" : "更新失败!");
        }

        /// <summary>
        /// 添加用户by CYQ
        /// </summary>
        /// <returns></returns>
        public string AddUser()
        {
            string jsonResult = string.Empty;
            bool result = false;
            string userName = Query<string>("userName");
            string pwd = Query<string>("Password");
            using (MAction action = new MAction(TableNames.Sys_User))
            {
                action.BeginTransation();
                if (!action.Exists("UserName = '" + userName + "'"))
                {
                    action.Set("Password", EncrpytHelper.Encrypt(pwd));//加密
                    if (action.Insert(true, InsertOp.ID))
                    {
                        string userID = action.Get<string>(Sys_User.UserID);

                        action.ResetTable(TableNames.Sys_UserInfo);
                        action.Set(Sys_UserInfo.UserInfoID, userID);
                        action.AllowInsertID = true;
                        result = action.Insert(true);
                        if (!result)
                        {
                            action.RollBack();
                        }
                        else
                        {
                            jsonResult = JsonHelper.OutResult(result, result ? "添加用户成功!" : "添加用户失败!");
                        }
                    }
                }
                else
                {
                    jsonResult = JsonHelper.OutResult(false, "帐号已存在,请重新输入");
                }
                action.EndTransation();
            }

            return jsonResult;
        }

        public string DeleteUser()
        {
            bool result = false;
            using (MAction action = new MAction(TableNames.Sys_User))
            {
                action.BeginTransation();
                result = action.Delete(GetID);
                if (result)
                {
                    action.ResetTable(TableNames.Sys_UserInfo);
                    if (action.Exists(GetID))
                    {
                        result = action.Delete(GetID);
                    }
                }
                if (!result)
                {
                    action.RollBack();
                }
                action.EndTransation();
            }
            return JsonHelper.OutResult(result, result ? "删除成功!" : "删除失败!");
        }

        #endregion


        #region 菜单权限相关操作
        /// <summary>
        /// 获取菜单by luoshushi
        /// </summary>
        /// <returns></returns>
        public string GetMenuJson()
        {
            string result = string.Empty;
            using (MAction action = new MAction(TableNames.Sys_Menu))
            {
                result = action.Select("ORDER BY menulevel ASC,sortorder asc").ToJson();
            }
            return result;
        }
        /// <summary>
        /// 获取所有权限 by luoshushi
        /// </summary>
        /// <returns></returns>
        public string GetActions()
        {
            string result = string.Empty;
            using (MAction action = new MAction(TableNames.Sys_Action))
            {
                result = action.Select().ToJson();
            }
            return result;
        }

        /// <summary>
        /// 获取菜单详细数据
        /// luoshushi
        /// </summary>
        /// <returns></returns>
        public string GetMenuDetails()
        {
            string result = string.Empty;
            string id = Query<string>("id");
            using (MAction action = new MAction(TableNames.Sys_Menu))
            {
                if (action.Fill(id))
                {
                    result = action.Data.ToJson();
                }
            }
            return result;
        }

        /// <summary>
        /// 删除菜单
        /// luoshushi
        /// </summary>
        /// <returns></returns>
        public string DeleteMenu()
        {
            bool result = false;
            string id = Query<string>("id");
            using (MAction action = new MAction(TableNames.Sys_Menu))
            {
                action.SetSelectColumns("MenuID", "ParentMenuID");
                MDataTable dt = action.Select();
                StringBuilder sb = new StringBuilder();
                sb.Append("'" + id + "',");
                GetChildrenID(dt, id, sb, "ParentMenuID");
                string where = "MenuID in (" + sb.ToString().TrimEnd(',') + ")";
                result = action.Delete(where);
                if (result)
                {
                    action.ResetTable(TableNames.Sys_RoleAction);//删除权限的设置
                    action.Delete(where);
                }
            }
            return JsonHelper.OutResult(result, result ? "删除成功" : "删除失败");
        }
        /// <summary>
        /// 验证菜单是否有子节点
        /// luoshushi
        /// </summary>
        /// <returns></returns>
        public string ValidMenuHasChlid()
        {
            bool result = false;
            string MenuID = Query<string>("MenuID");
            using (MAction action = new MAction(TableNames.Sys_Menu))
            {
                if (action.Fill("ParentMenuID='" + MenuID + "'"))
                {
                    result = true;
                }
            }
            return JsonHelper.OutResult(result, "");
        }
        /// <summary>
        /// 获取系统图标
        /// luoshushi
        /// </summary>
        /// <param name="dir_path"></param>
        /// <returns></returns>
        public string GetIconsPath(string dir_path)
        {
            StringBuilder sb = new StringBuilder("[");
            Regex reg = new Regex("^.*\\.gif$");
            if (Directory.Exists(dir_path))
            {
                string[] files = Directory.GetFiles(dir_path);
                //var list = Directory.EnumerateFiles(dir_path).ToList<string>();

                foreach (var fileName in files)
                {
                    if (reg.IsMatch(fileName)) { continue; }
                    sb.Append("\"").Append(fileName.Substring(fileName.LastIndexOf("\\") + 1)).Append("\"").Append(",");
                }
            }
            var str = sb.ToString();
            if (str.LastIndexOf(",") != -1)
            {
                str = str.Substring(0, str.LastIndexOf(",")) + "]";
            }
            else
            {
                str = str + "]";
            }
            return str;
        }
        /// <summary>
        /// 添加权限
        /// </summary>
        /// <returns></returns>
        public string AddPromission()
        {
            string strArr = Query<string>("data");
            string roleID = Query<string>("RoleID");
            bool result = false;
            MDataColumn mdc = new MDataColumn();
            mdc.Add("RoleID", SqlDbType.NVarChar);
            mdc.Add("MenuID", SqlDbType.NVarChar);
            mdc.Add("ActionID", SqlDbType.NVarChar);
            MDataTable dt = MDataTable.CreateFrom(strArr, mdc);

            if (dt != null && dt.Rows.Count > 0)
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    MDataRow row = dt.Rows[i];
                    if (row["MenuID"].IsNullOrEmpty || row["ActionID"].IsNullOrEmpty)
                    {
                        dt.Rows.RemoveAt(i);
                        i--;
                        continue;
                    }
                    row.Set("RoleID", roleID);
                }
                dt.TableName = "System_RoleAction";
                //删除该角色下面所有权限
                using (MAction action = new MAction(TableNames.Sys_RoleAction))
                {
                    action.BeginTransation();
                    action.Delete("RoleID='" + roleID + "'");
                    dt.DynamicData = action;
                    result = dt.AcceptChanges(AcceptOp.Insert);
                    if (!result)
                    {
                        action.RollBack();
                    }
                    action.EndTransation();
                }
            }

            return JsonHelper.OutResult(result, result ? "添加成功！" : "添加失败！");
        }
        public string GetMenuAndAction()
        {
            return SysMenu.SysMenuAction.ToJson();
        }

        public string GetMenuIDsandActionIds()
        {
            string RoleID = Query<String>("RoleID");
            MDataTable raDt = null;
            using (MAction action = new MAction(TableNames.Sys_RoleAction))
            {
                raDt = action.Select("RoleID ='" + RoleID + "'");
            }
            Dictionary<string, string> dic = SysMenu.RoleActionToDic(raDt, true);
            return JsonHelper.ToJson(dic);
        }
        /// <summary>
        /// 新增菜单(重写 返回个ID)
        /// </summary>
        /// <returns></returns>
        public string AddMenu()
        {

            bool result = false;
            string msg = string.Empty;
            using (MAction action = new MAction(ObjName))
            {
                if (Query<int>("MenuLevel") == 1)
                {
                    action.Set("ParentMenuID", DBNull.Value);
                }
                result = action.Insert(true, InsertOp.ID);
                if (result)
                {
                    msg = action.Get<string>(action.Data.PrimaryCell.ColumnName);
                }
                else if (AppDebug.OpenDebugInfo)
                {
                    Log.WriteLogToTxt(action.DebugInfo);
                }
            }
            return JsonHelper.OutResult(result, result ? msg : "添加失败!");
        }
        #endregion

  
        private void GetChildrenID(MDataTable dt, string parentID, StringBuilder sb, string parentName = "ParentID")
        {
            if (!string.IsNullOrEmpty(parentID))
            {
                List<MDataRow> rows = dt.FindAll(parentName + "='" + parentID + "'");
                if (rows != null)
                {
                    string id = string.Empty;
                    foreach (MDataRow row in rows)
                    {
                        id = row.Get<string>(0);
                        sb.Append("'" + id + "',");
                        GetChildrenID(dt, id, sb, parentName);
                    }
                }
            }
        }
    }
}
