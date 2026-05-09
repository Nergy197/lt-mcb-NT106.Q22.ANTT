import os
import json

path_to_moves = './data/api/v2/move/'
output_file = 'moves_final.json'
all_moves = []

print("--- Đang quét kho chiêu thức, anh đợi em xíu ---")

for root, dirs, files in os.walk(path_to_moves):
    for filename in files:
        if filename == 'index.json':
            file_path = os.path.join(root, filename)
            try:
                with open(file_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                    # Gộp toàn bộ chiêu thức, xử lý các giá trị null
                    cleaned = {
                        "id": data['id'],
                        "name": data['name'],
                        "power": data.get('power', 0) if data.get('power') is not None else 0,
                        "accuracy": data.get('accuracy', 100) if data.get('accuracy') is not None else 100,
                        "type": data['type']['name'],
                        "pp": data.get('pp', 0),
                        "priority": data.get('priority', 0)
                    }
                    all_moves.append(cleaned) # Phải nằm thẳng hàng với cleaned
            except: 
                continue

# Sắp xếp lại theo ID cho ngăn nắp
all_moves.sort(key=lambda x: x['id'])

with open(output_file, 'w', encoding='utf-8') as f:
    json.dump(all_moves, f, ensure_ascii=False, indent=4)

print("-" * 40)
print(f"XONG RỒI ANH ƠI! Đã gộp {len(all_moves)} chiêu thức.")
print(f"File báu vật thứ hai: {output_file}")
print("-" * 40)