local PathUtil = {}
local lfs = require("lfs")
local unicode = require("Modules.unicode")
local u2a = unicode.u2a

local function getAttr(path)
    return lfs.attributes(path) or lfs.attributes(u2a(path))
end

local function dirIterator(path)
    local ok, iter = pcall(lfs.dir, path)
    if ok then return iter end
    ok, iter = pcall(lfs.dir, u2a(path))
    if ok then return iter end
    error("Failed to open dir " .. tostring(path))
end

function PathUtil.listDirContents(dirPath)
    local dirContents = {}
    for entry in dirIterator(dirPath) do
        if entry ~= '.' and entry ~= '..' then
            table.insert(dirContents, entry)
        end
    end
    return dirContents
end

function PathUtil.dirExist(path)
    local attr = getAttr(path)
    return type(attr) == "table" and attr.mode == "directory"
end

function PathUtil.fileExist(path)
    local attr = getAttr(path)
    return type(attr) == "table" and attr.mode == "file"
end

function PathUtil.dirName(path)
    return path:match([[.+[/\]([^/\]+)[/\]?$]])
end

function PathUtil.dirPath(path)
    if path:find("%.%w+$") then
        return path:match([[^(.+)[/\][^/\]*%.%w+$]])
    else
        return path:match([[^(.-)[/\]?$]])
    end
end

function PathUtil.fileName(path)
    return path:match([[.+[/\]([^/\]+)$]])
end

function PathUtil.getExtension(path)
    if type(path) ~= "string" then
        return nil
    end
    return path:match("(%.%w+)$")
end

function PathUtil.fileNameWithoutExtension(path)
    return PathUtil.fileName(path):sub(1, -(1 + #(PathUtil.getExtension(path) or "")))
end

function PathUtil.deleteDir(rootpath)
    local function deleteEntry(entry)
        local path = rootpath .. '/' .. entry
        local attr = getAttr(path)
        assert(type(attr) == 'table', "Failed to get attributes for " .. path)

        if attr.mode == 'directory' then
            PathUtil.deleteDir(path)
        else
            assert(os.remove(path), "Failed to remove file " .. path)
        end
    end

    for entry in dirIterator(rootpath) do
        if entry ~= '.' and entry ~= '..' then
            deleteEntry(entry)
        end
    end
    assert(lfs.rmdir(rootpath) or lfs.rmdir(u2a(rootpath)), "Failed to remove directory " .. rootpath)
end

return PathUtil
