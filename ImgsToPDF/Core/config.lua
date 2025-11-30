local lfs = require("lfs")

local common = require("Modules.Common")
local pathUtil = require("Modules.PathUtil")
local unicode = require("Modules.unicode")
local osUtil = require("Modules.OSUtil")
local u2a = unicode.u2a
local a2u = unicode.a2u

local iPageSize = CS.iTextSharp.text.PageSize
local iRectangle = CS.iTextSharp.text.Rectangle
local commonUtils = CS.ImgsToPDFCore.CommonUtils
local PDFWrapper = CS.ImgsToPDFCore.PDFWrapper
local interaction = CS.Microsoft.VisualBasic.Interaction

-- 清理文件名中的非法字符，避免 Windows 写入失败
local function sanitizeName(name)
    if not name then return "output" end
    local cleaned = name:gsub('[\\\\/:%*%?"<>|]', "_")
    cleaned = cleaned:gsub("%s+$", ""):gsub("%.+$", "")
    if #cleaned == 0 then
        cleaned = "output"
    end
    if #cleaned > 180 then
        cleaned = cleaned:sub(1, 180)
    end
    return cleaned
end

-- add your local funcs below
-- 建议在这个部分添加你自己要用到的函数
local function getChildImgsAndDirs(dirPath)
    local imageExtensions = { ".png", ".apng", ".jpg", ".jpeg", ".jfif", ".pjpeg", ".pjp", ".bmp", ".tif", ".tiff",
        ".gif", ".webp" }
    local imgPaths = {}
    local dirPaths = {}
    local function getAttr(path)
        return lfs.attributes(path) or lfs.attributes(u2a(path))
    end

    local ok, iter = pcall(lfs.dir, dirPath)
    if not ok then
        ok, iter = pcall(lfs.dir, u2a(dirPath))
    end
    if not ok then
        return imgPaths, dirPaths
    end

    for entry in iter do
        if entry ~= '.' and entry ~= '..' then
            local path = dirPath .. '/' .. entry
            local attr = getAttr(path)
            assert(type(attr) == 'table')
            if attr.mode == 'directory' then
                table.insert(dirPaths, path)
            elseif common.hasVal(imageExtensions, pathUtil.getExtension(path)) then
                table.insert(imgPaths, path)
            end
        end
    end
    return imgPaths, dirPaths
end

-------------------------------------------------------------------
----***************************************************************
----Config for how to generate your images to pdf file
----图片转PDF的配置
----***************************************************************
-------------------------------------------------------------------

local Config = {}

-- the path to save your output pdf file
-- 输出PDF档的保存路径
-- @type string
local pdfFileName
local outputDir
function Config.PathToSave()
    return table.concat({ outputDir, "/", pdfFileName, ".pdf" })
end

-- page size of the output pdf file
-- 输出PDF档的页大小
-- @type iTextSharp.text.Rectangle
-- e.g. Config.PageSizeToSave = iPageSize.A4 (支持NoResize, A0~A10, B0~B10等)
-- 或 Config.PageSizeToSave = iRectangle(0, 0, width, height)
Config.PageSizeToSave = iPageSize.NoResize

-- func that you can order your input files
-- 图片文件排序的方法，默认会去找文件名中的数字部分来进行排序
-- @param path1, path2: string; Full file path of the files to compare.
-- @return: int; If negative, file in path1 will be added to your pdf first.
function Config:FilePathComparer(filePath1, filePath2)
    -- 从完整路径中截取文件名
    local fileName1 = pathUtil.fileNameWithoutExtension(filePath1) or ""
    local fileName2 = pathUtil.fileNameWithoutExtension(filePath2) or ""
    -- 获取文件名中的数字部分
    local pattern = "(%d+)[^%d]-$"
    local numInPath1, numInPath2 = tonumber(fileName1:match(pattern)), tonumber(fileName2:match(pattern))

    if not numInPath1 and not numInPath2 then -- 如果二者都没有找到数字部分，采用默认排序决定
        return filePath1 == filePath2 and 0 or filePath1 < filePath2 and -1 or 1
    else                                      -- 若其中之一无数字，无数字者在前；否则数字小者在前
        return (numInPath1 or -1) - (numInPath2 or -1)
    end
end

local tempExtraPath
-- this func will be processed before pdf generation start
-- 定义开始前要进行的动作
function Config:PreProcess(...)
    local path, layout, fastFlag, fileList = table.unpack({ ... })
    local compressSuffix = { ".zip", ".rar", ".7z" }
    if fileList and pathUtil.fileExist(fileList) then
        local imgList = {}
        for line in io.lines(fileList) do
            local trimmed = line:gsub("^%s+", ""):gsub("%s+$", "")
            if trimmed ~= '' and pathUtil.fileExist(trimmed) then
                table.insert(imgList, trimmed)
            end
        end
        if next(imgList) then
            table.sort(imgList, function(a, b) return Config:FilePathComparer(a, b) < 0 end)
            local firstDir = pathUtil.dirPath(imgList[1]) or ""
            pdfFileName = sanitizeName(pathUtil.dirName(firstDir) or pathUtil.fileNameWithoutExtension(imgList[1]) or "output")
            outputDir = firstDir ~= "" and firstDir or (pathUtil.dirPath(imgList[1]) or ".")
            PDFWrapper.ImagesToPDF(imgList, layout, fastFlag)
        end
        return
    end
    if not path or common.isEmpty(path) then
        return
    end
    if pathUtil.dirExist(path) then -- 如果是文件夹
        pdfFileName = sanitizeName(pathUtil.dirName(path))
        outputDir = path
        PDFWrapper.ImagesToPDF(path, layout, fastFlag)
        return
    end

    local ext = pathUtil.getExtension(path)
    if not ext then
        return
    end

    if not common.hasVal(compressSuffix, ext:lower()) then
        return -- 不以压缩格式结尾 不做动作
    end

    pdfFileName = sanitizeName(pathUtil.fileNameWithoutExtension(path))
    outputDir = pathUtil.dirPath(path)
    tempExtraPath = path:sub(1, -(1 + #ext)) .. os.date("%Y%m%d%H%M%S")
    if not commonUtils.Decompress(path, tempExtraPath) then
        local password = interaction.InputBox("Input password:", "Encrypted Compress File")
        if common.isEmpty(password) or not commonUtils.Decompress(path, tempExtraPath, password) then
            return
        end
    end

    local childImgs, childDirs = getChildImgsAndDirs(tempExtraPath)
    if not next(childImgs) then
        if next(childDirs) then
            PDFWrapper.ImagesToPDF(childDirs[1], layout, fastFlag)
        end
        return
    end
    PDFWrapper.ImagesToPDF(tempExtraPath, layout, fastFlag)
end

-- this func will be processed after your pdf generated
-- 定义结束后要进行的动作
function Config:PostProcess()
    if tempExtraPath and pathUtil.dirExist(tempExtraPath) then
        pathUtil.deleteDir(tempExtraPath)
    end
end

return Config
