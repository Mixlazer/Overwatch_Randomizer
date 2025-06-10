import tkinter as tk
from tkinter import ttk
import random
from PIL import Image, ImageTk
import os

# English roles
roles = ['Tank', 'Damage', 'Support']

# Full roster by role
characters_by_role_full = {
    'Tank': [
        'D.Va', 'Junker Queen', 'Orisa', 'Reinhardt', 'Zarya', 'Winston', 'Sigma', 'Ramattra', 'Roadhog', 'Mauga', 'Wrecking Ball'
    ],
    'Damage': [
        'Ashe', 'Cassidy', 'Freja', 'Genji', 'Mei', 'Reaper', 'Soldier: 76', 'Echo', 'Pharah', 'Sojourn', 'Sombra', 'Symmetra', 'Torbjörn', 'Tracer', 'Hanzo', 'Bastion', 'Junkrat'
    ],
    'Support': [
        'Ana', 'Juno', 'Kiriko', 'Lúcio', 'Mercy', 'Moira', 'Zenyatta', 'Baptiste', 'Brigitte', 'Illari', 'Lifeweaver'
    ]
}

# Limited Stadium roster
stadium_characters = [
    'D.Va', 'Junker Queen', 'Orisa', 'Reinhardt', 'Zarya',
    'Ashe', 'Cassidy', 'Freja', 'Genji', 'Mei', 'Reaper', 'Soldier: 76',
    'Ana', 'Juno', 'Kiriko', 'Lúcio', 'Mercy', 'Moira'
]

# Modes to count mapping
mode_to_count = {
    '5v5': 5,
    'Open': 6,
    'Stadium': 5
}

# Role limits per mode
role_constraints = {
    '5v5': {'Tank': 1, 'Damage': 2, 'Support': 2},
    'Stadium': {'Tank': 1, 'Damage': 2, 'Support': 2},
    'Open': {'Tank': 2, 'Damage': 2, 'Support': 2}
}

class OverwatchRandomizerApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Overwatch Randomizer")

        self.mode_var = tk.StringVar(value='5v5')
        self.roles_labels = []
        self.character_labels = []
        self.avatar_labels = []
        self.avatar_images = []  # Store references to prevent garbage collection

        self.create_widgets()
        self.update_count('5v5')
        self.generate_roles()
        self.generate_characters()

    def create_widgets(self):
        ttk.Label(self.root, text="Mode:").grid(row=0, column=1)
        ttk.OptionMenu(self.root, self.mode_var, '5v5', *mode_to_count.keys(), command=self.change_mode).grid(row=0, column=2)

        self.role_frame = tk.Frame(self.root)
        self.role_frame.grid(row=1, column=0, columnspan=3)

        self.button_roles = ttk.Button(self.root, text="Generate Roles", command=self.generate_roles)
        self.button_roles.grid(row=2, column=0, sticky='w')

        self.button_characters = ttk.Button(self.root, text="Generate Characters", command=self.generate_characters)
        self.button_characters.grid(row=3, column=0, sticky='w')

    def change_mode(self, mode):
        self.update_count(mode)
        self.generate_roles()
        self.generate_characters()

    def update_count(self, mode):
        count = mode_to_count[mode]

        for widget in self.role_frame.winfo_children():
            widget.destroy()

        self.roles_labels = []
        self.character_labels = []
        self.avatar_labels = []
        self.avatar_images = []

        for i in range(count):
            lbl_role = ttk.Label(self.role_frame, text="Role")
            lbl_role.grid(row=i, column=0, padx=5, pady=5)
            self.roles_labels.append(lbl_role)

            lbl_char = ttk.Label(self.role_frame, text="Character")
            lbl_char.grid(row=i, column=1, padx=5, pady=5)
            self.character_labels.append(lbl_char)

            lbl_img = tk.Label(self.role_frame)  # Use tk.Label instead of ttk.Label
            lbl_img.grid(row=i, column=2, padx=5, pady=5)
            self.avatar_labels.append(lbl_img)
            self.avatar_images.append(None)

    def generate_roles(self):
        count = len(self.roles_labels)
        mode = self.mode_var.get()
        limits = role_constraints[mode].copy()

        available_roles = sum(([role] * qty for role, qty in limits.items()), [])
        if count > len(available_roles):
            available_roles.extend(random.choices(roles, k=count - len(available_roles)))
        random.shuffle(available_roles)

        self.generated_roles = available_roles[:count]

        for i, role in enumerate(self.generated_roles):
            self.roles_labels[i].config(text=role)

    def generate_characters(self):
        selected_characters = set()
        mode = self.mode_var.get()

        for i, lbl in enumerate(self.roles_labels):
            role = lbl.cget("text")
            if mode == 'Stadium':
                pool = [ch for ch in stadium_characters if ch in characters_by_role_full[role]]
            else:
                pool = characters_by_role_full[role]

            pool = list(set(pool) - selected_characters)

            if not pool:
                character = '???'
            else:
                character = random.choice(pool)
                selected_characters.add(character)

            self.character_labels[i].config(text=character)

            img_file = f"Icon-{character.replace(': ', '').replace(':', '').replace(' ', '_')}.webp"
            BASE_DIR = os.path.dirname(__file__)
            img_path = os.path.join(BASE_DIR, 'assets', img_file)
            #print(f"Looking for: {img_path}")
            if os.path.exists(img_path):
                try:
                    img = Image.open(img_path).convert("RGBA")
                    img = img.resize((50, 50), Image.LANCZOS)
                    photo = ImageTk.PhotoImage(img)
                    self.avatar_labels[i].configure(image=photo)
                    self.avatar_labels[i].image = photo
                    self.avatar_images[i] = photo
                except Exception as e:
                    #print(f"Failed to load {img_path}: {e}")
                    self.avatar_labels[i].configure(image='', text='')
                    self.avatar_images[i] = None
            else:
                self.avatar_labels[i].configure(image='', text='')
                self.avatar_images[i] = None

if __name__ == '__main__':
    root = tk.Tk()
    app = OverwatchRandomizerApp(root)
    root.mainloop()

