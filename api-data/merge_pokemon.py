import os
import json

# Đường dẫn đến kho dữ liệu trong repo anh đã clone
path_to_data = './data/api/v2/pokemon/'
output_file = 'pokedex_final.json'
all_pokemons = []

print("--- Đang bắt đầu quét 1025 Pokemon, anh đợi em xíu nhé ---")

# Dùng os.walk để chui vào từng thư mục con (1, 2, 3...)
for root, dirs, files in os.walk(path_to_data):
    for filename in files:
        # Trong repo này, dữ liệu thực tế nằm ở file index.json bên trong mỗi folder
        if filename == 'index.json':
            file_path = os.path.join(root, filename)
            try:
                with open(file_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                    
                    # Chỉ lấy file có chứa ID (tránh các file rác)
                    if 'id' in data:
                        cleaned = {
                            "id": data['id'],
                            "name": data['name'],
                            "types": [t['type']['name'] for t in data['types']],
                            "base_stats": {s['stat']['name']: s['base_stat'] for s in data['stats']},
                            "sprite_url": data['sprites']['front_default'],
                            "height": data['height'],
                            "weight": data['weight']
                        }
                        all_pokemons.append(cleaned)
                        
                        if len(all_pokemons) % 100 == 0:
                            print(f"Đã xử lý xong {len(all_pokemons)} con...")
            except:
                continue

# Sắp xếp lại theo số thứ tự Pokemon (ID)
all_pokemons.sort(key=lambda x: x['id'])

with open(output_file, 'w', encoding='utf-8') as f:
    json.dump(all_pokemons, f, ensure_ascii=False, indent=4)

print("-" * 40)
print(f"THÀNH CÔNG! Đã gộp {len(all_pokemons)} con vào file: {output_file}")
print("-" * 40)