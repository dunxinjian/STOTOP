using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

public class PermissionService : IPermissionService
    {
        private readonly STOTOPDbContext _context;

        public PermissionService(STOTOPDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 将中文类型转换为英文类型（前端兼容）
        /// </summary>
        private static string ConvertTypeToEnglish(string chineseType)
        {
            return chineseType switch
            {
                "模块" => "module",
                "菜单" => "menu",
                "按钮" => "button",
                _ => chineseType // 如果不是已知的中文类型，原样返回
            };
        }

        public async Task<ApiResult<List<PermissionDto>>> GetTreeAsync()
        {
            var permissions = await _context.Set<SysPermission>()
                .OrderBy(p => p.FSort)
                .ThenBy(p => p.FCreateTime)
                .ToListAsync();

            var dtoList = permissions.Select(p => new PermissionDto
            {
                Id = p.FID,
                Name = p.FName,
                Code = p.FCode,
                Type = ConvertTypeToEnglish(p.FType),
                ParentId = p.FParentId,
                Route = p.FRoute,
                ComponentPath = p.FComponentPath,
                Icon = p.FIcon,
                Sort = p.FSort,
                Status = p.FStatus,
                IsVisible = p.FIsVisible,
                CreateTime = p.FCreateTime
            }).ToList();

            var tree = BuildPermissionTree(dtoList);
            return ApiResult<List<PermissionDto>>.Success(tree);
        }

        public async Task<ApiResult<List<MenuTreeResponse>>> GetMenuTreeAsync(long userId)
        {
            // 获取用户角色
            var roleIds = await _context.Set<SysUserRole>()
                .Where(ur => ur.FUserId == userId)
                .Select(ur => ur.FRoleId)
                .ToListAsync();

            // 获取用户权限ID列表
            var permissionIds = await _context.Set<SysRolePermission>()
                .Where(rp => roleIds.Contains(rp.FRoleId))
                .Select(rp => rp.FPermissionId)
                .Distinct()
                .ToListAsync();

            // 获取可见的菜单权限
            var permissions = await _context.Set<SysPermission>()
                .Where(p => permissionIds.Contains(p.FID) 
                    && p.FStatus == 1 
                    && p.FIsVisible == 1
                    && (p.FType == "模块" || p.FType == "菜单"))
                .OrderBy(p => p.FSort)
                .ToListAsync();

            var dtoList = permissions.Select(p => new MenuTreeResponse
            {
                Id = p.FID,
                Name = p.FName,
                Code = p.FCode,
                Route = p.FRoute,
                ComponentPath = p.FComponentPath,
                Icon = p.FIcon,
                Sort = p.FSort,
                Type = p.FType == "模块" ? "module" : (p.FType == "按钮" ? "button" : "menu"),
                Badge = 0 // 占位，后续可对接实际业务数据
            }).ToList();

            var tree = BuildMenuTree(dtoList);
            return ApiResult<List<MenuTreeResponse>>.Success(tree);
        }

        public async Task<ApiResult<PermissionDto>> CreateAsync(CreatePermissionRequest request)
        {
            if (await _context.Set<SysPermission>().AnyAsync(p => p.FCode == request.Code))
            {
                return ApiResult<PermissionDto>.Fail("权限编码已存在");
            }

            var permission = new SysPermission
            {
                FName = request.Name,
                FCode = request.Code,
                FType = request.Type,
                FParentId = request.ParentId,
                FRoute = request.Route,
                FComponentPath = request.ComponentPath,
                FIcon = request.Icon,
                FSort = request.Sort,
                FStatus = request.Status,
                FIsVisible = request.IsVisible
            };

            await _context.Set<SysPermission>().AddAsync(permission);
            await _context.SaveChangesAsync();

            var dto = new PermissionDto
            {
                Id = permission.FID,
                Name = permission.FName,
                Code = permission.FCode,
                Type = ConvertTypeToEnglish(permission.FType),
                ParentId = permission.FParentId,
                Route = permission.FRoute,
                ComponentPath = permission.FComponentPath,
                Icon = permission.FIcon,
                Sort = permission.FSort,
                Status = permission.FStatus,
                IsVisible = permission.FIsVisible,
                CreateTime = permission.FCreateTime
            };

            return ApiResult<PermissionDto>.Success(dto);
        }

        public async Task<ApiResult<PermissionDto>> UpdateAsync(long id, UpdatePermissionRequest request)
        {
            var permission = await _context.Set<SysPermission>().FindAsync(id);
            if (permission == null)
            {
                return ApiResult<PermissionDto>.Fail("权限不存在");
            }

            permission.FName = request.Name;
            permission.FRoute = request.Route;
            permission.FComponentPath = request.ComponentPath;
            permission.FIcon = request.Icon;
            permission.FSort = request.Sort;
            permission.FStatus = request.Status;
            permission.FIsVisible = request.IsVisible;
            permission.FUpdateTime = DateTime.Now;

            await _context.SaveChangesAsync();

            var dto = new PermissionDto
            {
                Id = permission.FID,
                Name = permission.FName,
                Code = permission.FCode,
                Type = ConvertTypeToEnglish(permission.FType),
                ParentId = permission.FParentId,
                Route = permission.FRoute,
                ComponentPath = permission.FComponentPath,
                Icon = permission.FIcon,
                Sort = permission.FSort,
                Status = permission.FStatus,
                IsVisible = permission.FIsVisible,
                CreateTime = permission.FCreateTime
            };

            return ApiResult<PermissionDto>.Success(dto);
        }

        public async Task<ApiResult<bool>> DeleteAsync(long id)
        {
            var permission = await _context.Set<SysPermission>().FindAsync(id);
            if (permission == null)
            {
                return ApiResult<bool>.Fail("权限不存在");
            }

            // 检查是否有子权限
            var hasChildren = await _context.Set<SysPermission>().AnyAsync(p => p.FParentId == id);
            if (hasChildren)
            {
                return ApiResult<bool>.Fail("请先删除子权限");
            }

            _context.Set<SysPermission>().Remove(permission);
            await _context.SaveChangesAsync();
            return ApiResult<bool>.Success(true);
        }

        public async Task<ApiResult<List<string>>> GetCurrentPermissionsAsync(long userId)
        {
            var roleIds = await _context.Set<SysUserRole>()
                .Where(ur => ur.FUserId == userId)
                .Select(ur => ur.FRoleId)
                .ToListAsync();

            var codes = await _context.Set<SysRolePermission>()
                .Where(rp => roleIds.Contains(rp.FRoleId))
                .Join(_context.Set<SysPermission>(),
                    rp => rp.FPermissionId,
                    p => p.FID,
                    (rp, p) => p.FCode)
                .Distinct()
                .ToListAsync();

            return ApiResult<List<string>>.Success(codes);
        }

        private static List<PermissionDto> BuildPermissionTree(List<PermissionDto> list)
        {
            var lookup = list.ToLookup(p => p.ParentId);
            var rootNodes = lookup[0].ToList();

            foreach (var node in rootNodes)
            {
                AddPermissionChildren(node, lookup);
            }

            return rootNodes;
        }

        private static void AddPermissionChildren(PermissionDto node, ILookup<long, PermissionDto> lookup)
        {
            var children = lookup[node.Id].ToList();
            node.Children = children;
            foreach (var child in children)
            {
                AddPermissionChildren(child, lookup);
            }
        }

        private static List<MenuTreeResponse> BuildMenuTree(List<MenuTreeResponse> list)
        {
            // 由于菜单没有 ParentId 字段在 DTO 中，这里简化处理
            // 实际应该查询完整的权限数据来构建树
            return list;
        }
    }
