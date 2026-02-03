import os
import sys
import json
import hashlib
import requests
import argparse
from datetime import datetime

# 配置常量
API_DOMAIN = "https://open-api.123pan.com"
PLATFORM = "open_platform"
# CHUNK_SIZE_DEFAULT = 10 * 1024 * 1024  # 默认10MB，实际由Create接口返回决定

def calculate_md5(file_path):
    """计算文件整体MD5"""
    print(f"Running: 计算文件MD5: {file_path}")
    hash_md5 = hashlib.md5()
    with open(file_path, "rb") as f:
        for chunk in iter(lambda: f.read(4096), b""):
            hash_md5.update(chunk)
    return hash_md5.hexdigest()

def calculate_slice_md5(file_handle, size):
    """计算分片MD5"""
    pos = file_handle.tell()
    chunk = file_handle.read(size)
    file_handle.seek(pos) # Reset position
    return hashlib.md5(chunk).hexdigest(), chunk

def get_access_token(client_id, client_secret):
    """获取 Access Token"""
    url = f"{API_DOMAIN}/api/v1/access_token"
    headers = {
        "Platform": PLATFORM,
        "Content-Type": "application/json"
    }
    data = {
        "clientID": client_id,
        "clientSecret": client_secret
    }
    
    resp = requests.post(url, headers=headers, json=data)
    if resp.status_code != 200:
        raise Exception(f"获取Token失败: {resp.text}")
        
    result = resp.json()
    if result.get("code") != 0:
        raise Exception(f"获取Token API错误: {result}")
        
    return result["data"]["accessToken"]

def create_file(token, parent_id, file_path, file_md5):
    """预创建文件"""
    url = f"{API_DOMAIN}/upload/v2/file/create"
    headers = {
        "Authorization": f"Bearer {token}",
        "Platform": PLATFORM,
        "Content-Type": "application/json"
    }
    
    file_size = os.path.getsize(file_path)
    filename = os.path.basename(file_path)
    
    data = {
        "parentFileID": int(parent_id),
        "filename": filename,
        "etag": file_md5,
        "size": file_size,
        "duplicate": 2, # 1: 自动重命名, 2: 覆盖 (我们选择覆盖)
        "containDir": False
    }
    
    resp = requests.post(url, headers=headers, json=data)
    result = resp.json()
    
    if result.get("code") != 0:
        raise Exception(f"创建文件失败: {result}")
        
    return result["data"]

def upload_slice(token, upload_url, preupload_id, slice_no, slice_md5, slice_data):
    """上传单个分片"""
    url = f"{upload_url}/upload/v2/file/slice"
    headers = {
        "Authorization": f"Bearer {token}",
        "Platform": PLATFORM
    }
    
    # 构造 multipart/form-data
    files = {
        "slice": (f"chunk_{slice_no}", slice_data)
    }
    data = {
        "preuploadID": preupload_id,
        "sliceNo": str(slice_no),
        "sliceMD5": slice_md5
    }
    
    resp = requests.post(url, headers=headers, data=data, files=files)
    result = resp.json()
    
    if result.get("code") != 0:
        if result.get("message") == "分片重复上传":
             print(f"Warning: Slice {slice_no} already exists, skipping.")
             return
        raise Exception(f"上传分片 {slice_no} 失败: {result}")

def upload_complete(token, upload_url, preupload_id):
    """完成上传"""
    url = f"{upload_url}/upload/v2/file/upload_complete"
    headers = {
        "Authorization": f"Bearer {token}",
        "Platform": PLATFORM,
        "Content-Type": "application/json"
    }
    data = {
        "preuploadID": preupload_id
    }
    
    resp = requests.post(url, headers=headers, json=data)
    result = resp.json()
    
    if result.get("code") != 0:
        raise Exception(f"完成上传失败: {result}")
        
    return result["data"]["fileID"]

def get_direct_link(token, file_id):
    """获取直链"""
    url = f"{API_DOMAIN}/api/v1/direct-link/url"
    headers = {
        "Authorization": f"Bearer {token}",
        "Platform": PLATFORM,
        # "Content-Type": "application/json" # GET请求一般无需 Content-Type
    }
    
    params = {
        "fileID": file_id
    }
    
    resp = requests.get(url, headers=headers, params=params)
    result = resp.json()
    
    if result.get("code") != 0:
        raise Exception(f"获取直链失败: {result}")
        
    return result["data"]["url"]

def main():
    parser = argparse.ArgumentParser(description="Upload file to 123Pan")
    parser.add_argument("--file", required=True, help="Path to the file to upload")
    parser.add_argument("--client_id", required=True, help="123Pan Client ID")
    parser.add_argument("--client_secret", required=True, help="123Pan Client Secret")
    parser.add_argument("--parent_id", required=True, default="29037672", help="Parent Directory ID")
    
    args = parser.parse_args()
    
    file_path = args.file
    if not os.path.exists(file_path):
        print(f"Error: File not found: {file_path}")
        sys.exit(1)

    try:
        # 1. 获取 Token
        print("Authenticating...")
        token = get_access_token(args.client_id, args.client_secret)
        print("Success: Access Token Received")
        
        # 2. 计算 文件 MD5
        md5 = calculate_md5(file_path)
        
        # 3. 预创建文件
        print("Initializing Upload...")
        create_info = create_file(token, args.parent_id, file_path, md5)
        
        file_id = None
        
        # 检查是否秒传
        if create_info.get("reuse") == True:
            print("Success: Instant Upload (秒传成功)!")
            file_id = create_info["fileID"]
        else:
            # 分片上传
            preupload_id = create_info["preuploadID"]
            slice_size = create_info["sliceSize"]
            # 上传域名可能在 servers 数组中
            # 文档没细说 servers 结构，假设是 string list，取第一个
            upload_host = create_info["servers"][0] if create_info.get("servers") else API_DOMAIN
            if not upload_host.startswith("http"):
                upload_host = "https://" + upload_host
            
            print(f"Uploading Slices to {upload_host} (Size: {slice_size})...")
            
            with open(file_path, "rb") as f:
                slice_no = 1
                while True:
                    # 读取分片
                    chunk = f.read(slice_size)
                    if not chunk:
                        break
                        
                    # 算分片MD5
                    chunk_md5 = hashlib.md5(chunk).hexdigest()
                    
                    print(f"  - Uploading Slice {slice_no} ({len(chunk)} bytes)...")
                    upload_slice(token, upload_host, preupload_id, slice_no, chunk_md5, chunk)
                    slice_no += 1
            
            # 完成上传
            print("Finalizing Upload...")
            file_id = upload_complete(token, upload_host, preupload_id)
            print("Success: Upload Completed!")

        # 4. 获取直链
        print(f"Fetching Direct Link for File ID: {file_id}")
        direct_link = get_direct_link(token, file_id)
        
        print("\n" + "="*30)
        print(f"DIRECT LINK: {direct_link}")
        print("="*30 + "\n")
        
        # 将直链写入 环境变量文件，供后续Step读取
        # GitHub Actions 中可以用 GITHUB_OUTPUT
        if "GITHUB_OUTPUT" in os.environ:
            with open(os.environ["GITHUB_OUTPUT"], "a") as f:
                f.write(f"download_url={direct_link}\n")
        
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
