import os
import requests
import json
import concurrent.futures

BASE_DIR = "/Users/nergy/lt-mcb-NT106.Q22.ANTT/Server/wwwroot/data"
POKEMON_FRONT = os.path.join(BASE_DIR, "pokemon/front")
POKEMON_BACK = os.path.join(BASE_DIR, "pokemon/back")
POKEMON_ICONS = os.path.join(BASE_DIR, "pokemon/icons")

def download_file(url, path):
    if os.path.exists(path): return
    try:
        r = requests.get(url, timeout=10)
        if r.status_code == 200:
            with open(path, 'wb') as f:
                f.write(r.content)
    except: pass

def sync_sprites():
    print("Fetching Pokemon species list...")
    # Fetch names and IDs for all 1025 pokemon
    r = requests.get("https://pokeapi.co/api/v2/pokemon-species?limit=1025").json()['results']
    
    tasks = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=10) as executor:
        for i, p in enumerate(r):
            name = p['name']
            dex_id = i + 1
            
            # Front
            tasks.append(executor.submit(download_file, f"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{dex_id}.png", os.path.join(POKEMON_FRONT, f"{name}.png")))
            # Back
            tasks.append(executor.submit(download_file, f"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/back/{dex_id}.png", os.path.join(POKEMON_BACK, f"{name}.png")))
            # Icon
            tasks.append(executor.submit(download_file, f"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/versions/generation-viii/icons/{dex_id}.png", os.path.join(POKEMON_ICONS, f"{name}.png")))
        
        print(f"Queued {len(tasks)} sprite downloads...")
        
    print("All sprites queued for download.")

if __name__ == "__main__":
    os.makedirs(POKEMON_FRONT, exist_ok=True)
    os.makedirs(POKEMON_BACK, exist_ok=True)
    os.makedirs(POKEMON_ICONS, exist_ok=True)
    sync_sprites()
