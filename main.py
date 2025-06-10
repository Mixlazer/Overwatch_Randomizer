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
    'Open': {'Tank': 2, 'Damage': 6, 'Support': 6}
}

class OverwatchRandomizerApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Overwatch Randomizer")

        # Variables
        self.mode_var = tk.StringVar(value='5v5')
        self.custom_count_var = tk.IntVar(value=1)
        self.custom_count_var.trace_add('write', self.on_custom_count_change)
        self.roles_labels = []
        self.character_labels = []
        self.avatar_labels = []
        self.avatar_images = []

        self.create_widgets()
        self.refresh()

    def create_widgets(self):
        # Mode label and option menu
        ttk.Label(self.root, text="Mode:").grid(row=0, column=0, sticky='w')
        options = list(mode_to_count.keys()) + ['Custom']
        self.option_menu = ttk.OptionMenu(
            self.root, self.mode_var, self.mode_var.get(), *options, command=self.on_mode_change
        )
        self.option_menu.grid(row=0, column=1, sticky='w')

        # Custom count entry (hidden by default)
        ttk.Label(self.root, text="Count (1-10):").grid(row=0, column=2, sticky='w')
        self.custom_label = self.root.grid_slaves(row=0, column=2)[0]
        self.custom_entry = ttk.Entry(self.root, width=5, textvariable=self.custom_count_var)
        self.custom_entry.grid(row=0, column=3, sticky='w')
        self.custom_label.grid_remove()
        self.custom_entry.grid_remove()

        # Frame for roles and characters
        self.role_frame = tk.Frame(self.root)
        self.role_frame.grid(row=1, column=0, columnspan=4)

        # Buttons
        self.button_roles = ttk.Button(self.root, text="Generate Roles", command=self.generate_roles)
        self.button_roles.grid(row=2, column=0, pady=5, sticky='w')
        self.button_characters = ttk.Button(self.root, text="Generate Characters", command=self.generate_characters)
        self.button_characters.grid(row=2, column=1, pady=5, sticky='w')

    def on_mode_change(self, mode):
        if mode == 'Custom':
            self.custom_label.grid()
            self.custom_entry.grid()
        else:
            self.custom_label.grid_remove()
            self.custom_entry.grid_remove()
        self.refresh()

    def on_custom_count_change(self, *args):
        if self.mode_var.get() == 'Custom':
            self.refresh()

    def refresh(self):
        """Rebuild UI and generate initial data"""
        self.update_count()
        self.generate_roles()
        self.generate_characters()

    def update_count(self):
        mode = self.mode_var.get()
        count = 1
        if mode == 'Custom':
            try:
                value = self.custom_count_var.get()
                count = int(value)
            except (ValueError, tk.TclError):
                count = 1
            count = max(1, min(10, count))
        else:
            count = mode_to_count.get(mode, 5)

        # Clear existing labels
        for widget in self.role_frame.winfo_children():
            widget.destroy()
        self.roles_labels.clear()
        self.character_labels.clear()
        self.avatar_labels.clear()
        self.avatar_images.clear()

        # Create new labels
        for i in range(count):
            lbl_role = ttk.Label(self.role_frame, text="Role")
            lbl_role.grid(row=i, column=0, padx=5, pady=5)
            self.roles_labels.append(lbl_role)

            lbl_char = ttk.Label(self.role_frame, text="Character")
            lbl_char.grid(row=i, column=1, padx=5, pady=5)
            self.character_labels.append(lbl_char)

            lbl_img = tk.Label(self.role_frame)
            lbl_img.grid(row=i, column=2, padx=5, pady=5)
            self.avatar_labels.append(lbl_img)
            self.avatar_images.append(None)

    def generate_roles(self):
        mode = self.mode_var.get()
        count = len(self.roles_labels)
        limits = role_constraints.get(mode, {}).copy()
        available_roles = []
        if limits:
            for role, qty in limits.items():
                available_roles.extend([role] * qty)
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
                pool = [ch for ch in stadium_characters if ch in characters_by_role_full.get(role, [])]
            else:
                pool = characters_by_role_full.get(role, [])
            pool = list(set(pool) - selected_characters)
            character = random.choice(pool) if pool else '???'
            selected_characters.add(character)
            self.character_labels[i].config(text=character)

            img_file = f"Icon-{character.replace(': ', '').replace(':', '').replace(' ', '_')}.webp"
            BASE_DIR = os.path.dirname(__file__)
            img_path = os.path.join(BASE_DIR, 'assets', img_file)
            if os.path.exists(img_path):
                try:
                    img = Image.open(img_path).convert("RGBA")
                    img = img.resize((50, 50), Image.LANCZOS)
                    photo = ImageTk.PhotoImage(img)
                    self.avatar_labels[i].configure(image=photo)
                    self.avatar_labels[i].image = photo
                    self.avatar_images[i] = photo
                except Exception as e:
                    print(f"Error loading image for {character}: {e}")
                    self.avatar_labels[i].configure(image='', text='')
                    self.avatar_images[i] = None
            else:
                self.avatar_labels[i].configure(image='', text='')
                self.avatar_images[i] = None

if __name__ == '__main__':
    root = tk.Tk()
    app = OverwatchRandomizerApp(root)
    root.mainloop()