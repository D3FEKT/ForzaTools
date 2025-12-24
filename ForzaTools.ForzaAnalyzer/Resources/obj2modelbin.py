import struct
import binascii
import os
import sys
import tkinter as tk
from tkinter import filedialog
import tkinter.font as tkFont
from tkinter import ttk
import io
import materials
from tag_data import *
from threading import Thread
from queue import Queue, Empty

overall_model_bounds = {
    'min_coord': None,
    'max_coord': None,
    'initialized': False
}

class TextRedirector(io.StringIO):
    def __init__(self, text_widget, tag="stdout"):
        self.text_widget = text_widget
        self.tag = tag
        super().__init__()

    def write(self, string):
        self.text_widget.configure(state="normal")
        self.text_widget.insert("end", string, (self.tag,))
        self.text_widget.see("end")  # Auto-scroll to the end
        self.text_widget.configure(state="disabled")
        self.text_widget.update_idletasks()  # Update the GUI

    def flush(self):
        pass

class App:
      def __init__(self, root):
        root.title("obj2modelbin")
        #setting window size
        width=900  # Increased width to accommodate the new list
        height=700  # Increased height to fit everything
        screenwidth = root.winfo_screenwidth()
        screenheight = root.winfo_screenheight()
        alignstr = '%dx%d+%d+%d' % (width, height, (screenwidth - width) / 2, (screenheight - height) / 2)
        root.geometry(alignstr)
        root.resizable(width=False, height=False)

        # Create main left and right frames
        left_frame = tk.Frame(root, width=400)
        left_frame.pack(side=tk.LEFT, fill=tk.BOTH, padx=5, pady=5, expand=False)
    
        right_frame = tk.Frame(root)
        right_frame.pack(side=tk.RIGHT, fill=tk.BOTH, padx=5, pady=5, expand=True)

        # Create control frame at the top of left frame
        control_frame = tk.Frame(left_frame, width=380, height=500)
        control_frame.pack(side=tk.TOP, fill=tk.BOTH, padx=5, pady=5)
        control_frame.pack_propagate(False)  # Prevent frame from shrinking

        # Create console frame at the bottom of left frame
        console_frame = tk.LabelFrame(left_frame, text="Console Output", padx=5, pady=5)
        console_frame.pack(side=tk.BOTTOM, fill=tk.BOTH, expand=True, padx=5, pady=5)

        # Create text widget for console output
        self.console = tk.Text(console_frame, wrap=tk.WORD, width=50, height=15, 
                              bg="#1e1e1e", fg="#f0f0f0", font=("Consolas", 9))
        self.console.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        # Add scrollbar to console
        console_scrollbar = tk.Scrollbar(console_frame, command=self.console.yview)
        console_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.console.configure(yscrollcommand=console_scrollbar.set)

        # Configure text tags
        self.console.tag_configure("stdout", foreground="#f0f0f0")
        self.console.tag_configure("stderr", foreground="#ff6b6b")
        self.console.tag_configure("info", foreground="#6bff6b")

        # Disable editing
        self.console.configure(state="disabled")

        # Redirect stdout to our console widget
        self.stdout_redirector = TextRedirector(self.console, "stdout")
        sys.stdout = self.stdout_redirector

        # Create object/group tree frame on the right side
        self.list_frame = tk.Frame(right_frame, width=300, height=400)
        self.list_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
    
        # Print a welcome message
        print("Console initialized.")
  

        # Create a label for the tree
        list_label = tk.Label(self.list_frame, text="Objects and Groups", font=("Times", 12, "bold"))
        list_label.pack(side=tk.TOP, anchor=tk.W)
        
        # Create a frame for the tree
        self.tree_frame = tk.Frame(self.list_frame)
        self.tree_frame.pack(side=tk.TOP, fill=tk.BOTH, expand=True, pady=5)
        
        # Create Treeview widget with checkbox column
        self.tree = ttk.Treeview(self.tree_frame, columns=("checked",))
        self.tree.heading('#0', text='Objects and Groups', anchor='w')
        self.tree.column('#0', width=250)
        self.tree.column('checked', width=30, anchor='center')
        self.tree.heading('checked', text='') 
        
        # Add vertical scrollbar to tree
        tree_scrollbar = ttk.Scrollbar(self.tree_frame, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=tree_scrollbar.set)
        
        # Pack the tree and scrollbar
        self.tree.pack(side="left", fill="both", expand=True)
        tree_scrollbar.pack(side="right", fill="y")
        
        # Selection dictionaries to track checked items
        self.selected_objects = {}
        self.selected_groups = {}

        # Put the material dropdown in the left control frame
        material_label = tk.Label(control_frame, text="Material:")
        material_label.pack(anchor=tk.W, pady=(10, 0))
        
        material_names = list(materials.materials.keys())  
        selected_material = tk.StringVar(value=material_names[0]) 
        material_dropdown = ttk.Combobox(control_frame, textvariable=selected_material, values=material_names)
        material_dropdown.pack(anchor=tk.W, fill=tk.X, pady=(0, 10))

        # Create a frame for all transformation options
        transform_frame = tk.LabelFrame(control_frame, text="Transformation Options", padx=5, pady=5)
        transform_frame.pack(fill=tk.X, pady=10)

        # Create subframes to organize the flipping options
        vertex_flip_frame = tk.Frame(transform_frame)
        vertex_flip_frame.pack(fill=tk.X, pady=(0, 5), anchor=tk.W)

        normal_flip_frame = tk.Frame(transform_frame)
        normal_flip_frame.pack(fill=tk.X, pady=(0, 5), anchor=tk.W)

        face_flip_frame = tk.Frame(transform_frame)
        face_flip_frame.pack(fill=tk.X, pady=(0, 5), anchor=tk.W)

        # Vertex flipping options
        vertex_flip_label = tk.Label(vertex_flip_frame, text="Flip Vertices:")
        vertex_flip_label.pack(side=tk.LEFT, padx=(0, 10))

        # Variables for vertex flipping
        self.flip_vertex_x = tk.BooleanVar()
        self.flip_vertex_y = tk.BooleanVar()
        self.flip_vertex_z = tk.BooleanVar()

        flip_vertex_x_check = tk.Checkbutton(vertex_flip_frame, text="X", variable=self.flip_vertex_x)
        flip_vertex_x_check.pack(side=tk.LEFT, padx=5)

        flip_vertex_y_check = tk.Checkbutton(vertex_flip_frame, text="Y", variable=self.flip_vertex_y)
        flip_vertex_y_check.pack(side=tk.LEFT, padx=5)

        flip_vertex_z_check = tk.Checkbutton(vertex_flip_frame, text="Z", variable=self.flip_vertex_z)
        flip_vertex_z_check.pack(side=tk.LEFT, padx=5)

        # Normal flipping options
        normal_flip_label = tk.Label(normal_flip_frame, text="Flip Normals:")
        normal_flip_label.pack(side=tk.LEFT, padx=(0, 10))

        # Variables for normal flipping
        self.flip_normal_x = tk.BooleanVar()
        self.flip_normal_y = tk.BooleanVar()
        self.flip_normal_z = tk.BooleanVar()

        flip_normal_x_check = tk.Checkbutton(normal_flip_frame, text="X", variable=self.flip_normal_x)
        flip_normal_x_check.pack(side=tk.LEFT, padx=5)

        flip_normal_y_check = tk.Checkbutton(normal_flip_frame, text="Y", variable=self.flip_normal_y)
        flip_normal_y_check.pack(side=tk.LEFT, padx=5)

        flip_normal_z_check = tk.Checkbutton(normal_flip_frame, text="Z", variable=self.flip_normal_z)
        flip_normal_z_check.pack(side=tk.LEFT, padx=5)

        # Face winding option
        self.flip_faces = tk.BooleanVar()
        flip_faces_check = tk.Checkbutton(face_flip_frame, text="Flip Face Winding Order", variable=self.flip_faces)
        flip_faces_check.pack(side=tk.LEFT, padx=(0, 5))

        # Keep the old variables for backward compatibility
        self.flip_x = self.flip_vertex_x
        self.flip_y = self.flip_vertex_y
        self.flip_z = self.flip_vertex_z


        # Add this right after the face_flip_frame and before any other frames in the transform_frame
        # Inside App.__init__, after face_flip_frame

        # Mirror frame
        mirror_frame = tk.Frame(transform_frame)
        mirror_frame.pack(fill=tk.X, pady=(0, 5), anchor=tk.W)

        mirror_label = tk.Label(mirror_frame, text="Mirror Mesh:")
        mirror_label.pack(side=tk.LEFT, padx=(0, 10))

        # Create a StringVar to store the selected mirror axis
        self.mirror_axis = tk.StringVar(value="X")

        # Create a dropdown for selecting the mirror axis
        mirror_axis_dropdown = ttk.Combobox(mirror_frame, textvariable=self.mirror_axis, 
                                           values=["X", "Y", "Z"], width=5, state="readonly")
        mirror_axis_dropdown.pack(side=tk.LEFT, padx=5)

        # Create a button to trigger mirroring
        mirror_button = tk.Button(mirror_frame, text="Mirror Mesh", command=self.mirror_selected_mesh)
        mirror_button.pack(side=tk.LEFT, padx=5)


        # Create a frame for scale and position controls
        scale_pos_frame = tk.LabelFrame(control_frame, text="Vertex Scale & Position", padx=5, pady=5)
        scale_pos_frame.pack(fill=tk.X, pady=10)

        # Add Scale controls
        scale_label = tk.Label(scale_pos_frame, text="Scale:")
        scale_label.grid(row=0, column=0, sticky="w", pady=(5,0))

        # Create variables to store the scale values
        self.scale_x = tk.DoubleVar(value=hex_to_float(mesh_data0["VertScaleX"]))
        self.scale_y = tk.DoubleVar(value=hex_to_float(mesh_data0["VertScaleY"]))
        self.scale_z = tk.DoubleVar(value=hex_to_float(mesh_data0["VertScaleZ"]))

        # X scale
        scale_x_label = tk.Label(scale_pos_frame, text="X:")
        scale_x_label.grid(row=1, column=0, sticky="w")
        scale_x_entry = tk.Entry(scale_pos_frame, textvariable=self.scale_x, width=8)
        scale_x_entry.grid(row=1, column=1, sticky="w", padx=2)

        # Y scale
        scale_y_label = tk.Label(scale_pos_frame, text="Y:")
        scale_y_label.grid(row=1, column=2, sticky="w")
        scale_y_entry = tk.Entry(scale_pos_frame, textvariable=self.scale_y, width=8)
        scale_y_entry.grid(row=1, column=3, sticky="w", padx=2)

        # Z scale
        scale_z_label = tk.Label(scale_pos_frame, text="Z:")
        scale_z_label.grid(row=1, column=4, sticky="w")
        scale_z_entry = tk.Entry(scale_pos_frame, textvariable=self.scale_z, width=8)
        scale_z_entry.grid(row=1, column=5, sticky="w", padx=2)

        # Add Position controls
        pos_label = tk.Label(scale_pos_frame, text="Position:")
        pos_label.grid(row=2, column=0, sticky="w", pady=(10,0))

        # Create variables to store the position values
        self.pos_x = tk.DoubleVar(value=hex_to_float(mesh_data0["VertPositionX"]))
        self.pos_y = tk.DoubleVar(value=hex_to_float(mesh_data0["VertPositionY"]))
        self.pos_z = tk.DoubleVar(value=hex_to_float(mesh_data0["VertPositionZ"]))

        # X position
        pos_x_label = tk.Label(scale_pos_frame, text="X:")
        pos_x_label.grid(row=3, column=0, sticky="w")
        pos_x_entry = tk.Entry(scale_pos_frame, textvariable=self.pos_x, width=8)
        pos_x_entry.grid(row=3, column=1, sticky="w", padx=2)

        # Y position
        pos_y_label = tk.Label(scale_pos_frame, text="Y:")
        pos_y_label.grid(row=3, column=2, sticky="w")
        pos_y_entry = tk.Entry(scale_pos_frame, textvariable=self.pos_y, width=8)
        pos_y_entry.grid(row=3, column=3, sticky="w", padx=2)

        # Z position
        pos_z_label = tk.Label(scale_pos_frame, text="Z:")
        pos_z_label.grid(row=3, column=4, sticky="w")
        pos_z_entry = tk.Entry(scale_pos_frame, textvariable=self.pos_z, width=8)
        pos_z_entry.grid(row=3, column=5, sticky="w", padx=2)
        # Selection buttons for objects and groups
        select_frame = tk.Frame(control_frame)
        select_frame.pack(fill=tk.X, pady=10)
        
        select_all_btn = tk.Button(select_frame, text="Select All", 
                                  command=self.select_all_items)
        select_all_btn.pack(side=tk.LEFT, padx=2, fill=tk.X, expand=True)
        
        deselect_all_btn = tk.Button(select_frame, text="Deselect All", 
                                    command=self.deselect_all_items)
        deselect_all_btn.pack(side=tk.RIGHT, padx=2, fill=tk.X, expand=True)

        # Add open and save buttons to control frame
        buttons_frame = tk.Frame(control_frame)
        buttons_frame.pack(fill=tk.X, pady=10)
        
        GButton_512 = tk.Button(buttons_frame)
        GButton_512["bg"] = "#f0f0f0"
        ft = tkFont.Font(family='Times', size=10)
        GButton_512["font"] = ft
        GButton_512["fg"] = "#000000"
        GButton_512["justify"] = "center"
        GButton_512["text"] = "Open"
        GButton_512.pack(side=tk.LEFT, padx=2, fill=tk.X, expand=True)
        GButton_512["command"] = lambda: self.load_obj_file()

        GButton_798 = tk.Button(buttons_frame)
        GButton_798["bg"] = "#f0f0f0"
        GButton_798["font"] = ft
        GButton_798["fg"] = "#000000"
        GButton_798["justify"] = "center"
        GButton_798["text"] = "Save"
        GButton_798.pack(side=tk.RIGHT, padx=2, fill=tk.X, expand=True)
        GButton_798["command"] = lambda: save_obj_to_binary(self, selected_material)
        
        # Store the loaded data
        self.loaded_data = None
        self.obj_structure = None
            # Add storage for model bounds
        self.model_bounds = {
            'min_coord': 0,
            'max_coord': 0
        }
        # Add tree item click handler
        self.tree.bind('<ButtonRelease-1>', self.on_tree_click)
      
      def __del__(self):
        sys.stdout = sys.__stdout__

      def load_obj_file(self):
          """Load an OBJ file and display its objects and groups"""
          result = get_file_path()
          if result:
              # Get the object and group structure from parse_obj result
              self.loaded_data = result
              vertices, faces, normals, uvs = result

              # Calculate and store model bounds for consistent scaling
              if vertices:
                  # Track mins and maxes for each axis separately
                  x_coords = [vertex[0] for vertex in vertices]
                  y_coords = [vertex[1] for vertex in vertices if len(vertex) > 1]
                  z_coords = [vertex[2] for vertex in vertices if len(vertex) > 2]
                  
                  min_x = min(x_coords)
                  max_x = max(x_coords)
                  min_y = min(y_coords)
                  max_y = max(y_coords)
                  min_z = min(z_coords)
                  max_z = max(z_coords)
                  
                  # Update global overall bounds if this is the first model or extends the current bounds
                  global overall_model_bounds
                  if not overall_model_bounds['initialized']:
                      overall_model_bounds = {
                          'min_x': min_x,
                          'max_x': max_x,
                          'min_y': min_y,
                          'max_y': max_y,
                          'min_z': min_z,
                          'max_z': max_z,
                          'initialized': True
                      }
                      print(f"Initialized overall model bounds: X={min_x} to {max_x}, Y={min_y} to {max_y}, Z={min_z} to {max_z}")
                  else:
                      # Expand bounds if needed to include this object
                      overall_model_bounds['min_x'] = min(overall_model_bounds['min_x'], min_x)
                      overall_model_bounds['max_x'] = max(overall_model_bounds['max_x'], max_x)
                      overall_model_bounds['min_y'] = min(overall_model_bounds['min_y'], min_y)
                      overall_model_bounds['max_y'] = max(overall_model_bounds['max_y'], max_y)
                      overall_model_bounds['min_z'] = min(overall_model_bounds['min_z'], min_z)
                      overall_model_bounds['max_z'] = max(overall_model_bounds['max_z'], max_z)
                      print(f"Updated overall model bounds: X={overall_model_bounds['min_x']} to {overall_model_bounds['max_x']}, "
                            f"Y={overall_model_bounds['min_y']} to {overall_model_bounds['max_y']}, "
                            f"Z={overall_model_bounds['min_z']} to {overall_model_bounds['max_z']}")
                  
                  # Calculate overall bound range for scaling
                  x_range = max_x - min_x
                  y_range = max_y - min_y
                  z_range = max_z - min_z
                  max_range = max(x_range, y_range, z_range)
                  
                  # Store this for consistent scaling
                  self.model_bounds = {
                      'min_coord': -max_range/2,
                      'max_coord': max_range/2
                  }
                  print(f"Set model bounds for uniform scaling: {self.model_bounds['min_coord']} to {self.model_bounds['max_coord']}")
              
              self.obj_structure = get_obj_structure(input_file)
              self.update_tree_view()

      
      def mirror_selected_mesh(self):
        """Mirror the currently loaded mesh along the selected axis"""
        if not self.loaded_data:
            print("No mesh loaded to mirror")
            return
        
        # Get the current mesh data
        vertices, faces, normals, uvs = self.loaded_data
        
        # Get the selected mirror axis
        axis = self.mirror_axis.get().lower()
        
        # Apply mirroring
        mirrored_vertices, mirrored_normals, mirrored_faces = mirror_mesh(
            vertices, normals, faces, axis)
        
        # Update the loaded data with the mirrored mesh
        self.loaded_data = (mirrored_vertices, mirrored_faces, mirrored_normals, uvs)
        
        # Notify user
        print(f"Mesh has been mirrored along the {axis.upper()} axis")
        
        # If we have structure data, update that as well
        if self.obj_structure:
            print("Note: Object/group structure preserved, but actual geometry has been mirrored")
      def update_tree_view(self):
          """Update the tree view with objects and their nested groups"""
          # Clear existing items
          for item in self.tree.get_children():
              self.tree.delete(item)
          
          self.selected_objects = {}
          self.selected_groups = {}
          
          if not self.obj_structure:
              return
              
          # Use the pre-built group_to_object mapping when available
          object_groups = {}
          
          # First attempt to use pre-calculated mapping
          if 'group_to_object' in self.obj_structure:
              # Initialize the object_groups structure
              for obj_name in self.obj_structure['objects']:
                  object_groups[obj_name] = []
                  
              # Populate with group info
              for group_name, obj_name in self.obj_structure['group_to_object'].items():
                  # Only add if the object exists and group has faces
                  if (obj_name in self.obj_structure['objects'] and 
                      group_name in self.obj_structure['group_faces']):
                      face_count = len(self.obj_structure['group_faces'][group_name])
                      object_groups[obj_name].append((group_name, face_count))
          else:
              # Fallback to the original algorithm - compute group-object relationships
              # Create a mapping of groups to their parent objects by finding overlaps
              group_mapping_cache = {}
              
              for group_name in self.obj_structure['groups']:
                  # Use cached result if available
                  if group_name in group_mapping_cache:
                      best_object = group_mapping_cache[group_name]
                  else:
                      # Find which object contains the most faces of this group
                      group_faces = set(self.obj_structure['group_faces'][group_name])
                      best_object = None
                      max_overlap = 0
                      
                      # Process objects in sorted order for deterministic results
                      for obj_name in sorted(self.obj_structure['objects'].keys()):
                          obj_faces = set(self.obj_structure['object_faces'][obj_name])
                          overlap = len(group_faces.intersection(obj_faces))
                          
                          if overlap > max_overlap:
                              max_overlap = overlap
                              best_object = obj_name
                      
                      # Cache the result
                      group_mapping_cache[group_name] = best_object
                  
                  # If we found a parent object, add the group to it
                  if best_object:
                      if best_object not in object_groups:
                          object_groups[best_object] = []
                      group_face_count = len(self.obj_structure['group_faces'][group_name])
                      object_groups[best_object].append((group_name, group_face_count))
          
          # Add objects to tree - sorting provides a consistent display
          for obj_name in sorted(self.obj_structure['objects'].keys()):
              face_count = self.obj_structure['objects'][obj_name]
              # Create a variable for the object checkbox
              var = tk.BooleanVar(value=True)  # Default to selected
              self.selected_objects[obj_name] = var
              
              # Add item to tree with checkbox
              obj_text = f"{obj_name} ({face_count} faces)"
              obj_id = self.tree.insert('', 'end', text=obj_text, open=True, values=("✓",))
              
              # Add groups that belong to this object - sort by name for consistency
              if obj_name in object_groups:
                  sorted_groups = sorted(object_groups[obj_name], key=lambda x: x[0])
                  for group_name, group_face_count in sorted_groups:
                      # Create a variable for the group checkbox
                      group_var = tk.BooleanVar(value=True)  # Default to selected
                      self.selected_groups[group_name] = group_var
                      
                      # Add group as child of object
                      group_text = f"{group_name} ({group_face_count} faces)"
                      self.tree.insert(obj_id, 'end', text=group_text, values=("✓",))
      
      def on_tree_click(self, event):
          """Handle clicks on the tree view"""
          region = self.tree.identify("region", event.x, event.y)
          if region == "cell":
              column = self.tree.identify_column(event.x)
              if column == "#1":  # The checkbox column
                  item = self.tree.identify_row(event.y)
                  if item:
                      # Get current checkbox state
                      current_value = self.tree.item(item, "values")[0]
                      # Toggle checkbox
                      new_value = "□" if current_value == "✓" else "✓"
                      self.tree.item(item, values=(new_value,))
                      
                      # Update the corresponding variable
                      is_checked = (new_value == "✓")
                      
                      # Get the name from the text (without face count)
                      item_text = self.tree.item(item, "text")
                      name = item_text.split(" (")[0]
                      
                      # Check if this is an object or group
                      is_object = self.tree.parent(item) == ""
                      
                      if is_object:
                          # Update object's checkbox state
                          if name in self.selected_objects:
                              self.selected_objects[name].set(is_checked)
                          
                          # Also update all child groups
                          for child in self.tree.get_children(item):
                              child_text = self.tree.item(child, "text")
                              child_name = child_text.split(" (")[0]
                              self.tree.item(child, values=(new_value,))
                              
                              if child_name in self.selected_groups:
                                  self.selected_groups[child_name].set(is_checked)
                      else:
                          # Update just this group's checkbox state
                          if name in self.selected_groups:
                              self.selected_groups[name].set(is_checked)
      
      def select_all_items(self):
          """Select all objects and groups"""
          # Update all object variables
          for name, var in self.selected_objects.items():
              var.set(True)
              
          # Update all group variables
          for name, var in self.selected_groups.items():
              var.set(True)
              
          # Update all checkboxes in the tree
          self._update_all_tree_checkboxes("✓")
      
      def deselect_all_items(self):
          """Deselect all objects and groups"""
          # Update all object variables
          for name, var in self.selected_objects.items():
              var.set(False)
              
          # Update all group variables
          for name, var in self.selected_groups.items():
              var.set(False)
              
          # Update all checkboxes in the tree
          self._update_all_tree_checkboxes("□")
          
      def _update_all_tree_checkboxes(self, value):
          """Helper to update all checkboxes in the tree"""
          # Update root items (objects)
          for obj_item in self.tree.get_children():
              self.tree.item(obj_item, values=(value,))
              
              # Update child items (groups)
              for group_item in self.tree.get_children(obj_item):
                  self.tree.item(group_item, values=(value,))
      
      def get_selected_faces(self):
          """Get list of face indices that are selected based on object/group selection"""
          if not self.obj_structure:
              return None
              
          selected_faces = set()
          
          # Add faces from selected objects (directly check the variables)
          for obj_name, var in self.selected_objects.items():
              if var.get() and obj_name in self.obj_structure['object_faces']:
                  selected_faces.update(self.obj_structure['object_faces'][obj_name])
                  
          # Add faces from selected groups (directly check the variables)
          for group_name, var in self.selected_groups.items():
              if var.get() and group_name in self.obj_structure['group_faces']:
                  selected_faces.update(self.obj_structure['group_faces'][group_name])
                  
          return sorted(list(selected_faces))




def get_obj_structure(obj_path):
    """Extract object and group structure from an OBJ file"""
    objects = {}  # {object_name: face count}
    groups = {}   # {group_name: face count}
    object_faces = {}  # {object_name: [face indices]}
    group_faces = {}   # {group_name: [face indices]}
    group_to_object = {}  # {group_name: object_name} mapping
    
    current_object = "default"
    current_group = "default"
    object_faces[current_object] = []
    group_faces[current_group] = []
    
    face_count = 0
    
    with open(obj_path, 'r') as f:
        for line in f:
            tokens = line.split()
            if not tokens:
                continue
                
            if tokens[0] == 'o':  # Object definition
                current_object = ' '.join(tokens[1:]) if len(tokens) > 1 else f"unnamed_object_{len(objects)}"
                if current_object not in object_faces:
                    object_faces[current_object] = []
                # Mark the current group as belonging to this object
                if current_group != "default":
                    group_to_object[current_group] = current_object
                    
            elif tokens[0] == 'g':  # Group definition
                current_group = ' '.join(tokens[1:]) if len(tokens) > 1 else f"unnamed_group_{len(groups)}"
                if current_group not in group_faces:
                    group_faces[current_group] = []
                # Associate this group with the current object
                group_to_object[current_group] = current_object
                    
            elif tokens[0] == 'f':
                try:
                    # Track face in current object and group
                    object_faces[current_object].append(face_count)
                    group_faces[current_group].append(face_count)
                    face_count += 1
                except ValueError:
                    pass
    
    # Calculate counts for each object and group
    for obj_name, faces in object_faces.items():
        objects[obj_name] = len(faces)
        
    for group_name, faces in group_faces.items():
        groups[group_name] = len(faces)
    
    return {
        'objects': objects,
        'groups': groups,
        'object_faces': object_faces,
        'group_faces': group_faces,
        'group_to_object': group_to_object,
        'total_faces': face_count
    }



        



def hex_to_float(hex_string):
    """Convert hex string to float"""
    # Remove spaces from hex string
    hex_string = hex_string.replace(" ", "")
    # Convert hex to bytes
    bytes_data = binascii.unhexlify(hex_string)
    # Unpack bytes to float (little-endian format)
    return struct.unpack('<f', bytes_data)[0]

def float_to_hex(float_value):
    """Convert float to hex string"""
    # Pack float to bytes (little-endian format)
    bytes_data = struct.pack('<f', float_value)
    # Convert bytes to hex string with spaces
    hex_string = binascii.hexlify(bytes_data).decode('ascii').upper()
    # Insert spaces every 2 characters
    return ' '.join(hex_string[i:i+2] for i in range(0, len(hex_string), 2))



def flip_vertices(vertices, flip_x, flip_y, flip_z):
    """
    Flip vertices along specified axes
    
    Parameters:
    vertices -- list of vertices to flip
    flip_x -- whether to flip along X axis
    flip_y -- whether to flip along Y axis
    flip_z -- whether to flip along Z axis
    
    Returns:
    Flipped vertices
    """
    flipped_vertices = []
    
    for vertex in vertices:
        new_vertex = vertex.copy()
        if flip_x and len(new_vertex) > 0:
            new_vertex[0] = -new_vertex[0]
        if flip_y and len(new_vertex) > 1:
            new_vertex[1] = -new_vertex[1]
        if flip_z and len(new_vertex) > 2:
            new_vertex[2] = -new_vertex[2]
        flipped_vertices.append(new_vertex)
    
    return flipped_vertices

def flip_normals(normals, flip_x, flip_y, flip_z):
    """
    Flip normals along specified axes
    
    Parameters:
    normals -- list of normals to flip
    flip_x -- whether to flip along X axis
    flip_y -- whether to flip along Y axis
    flip_z -- whether to flip along Z axis
    
    Returns:
    Flipped normals
    """
    flipped_normals = []
    
    for normal in normals:
        x_normal, y_normal, z_normal = normal
        if flip_x:
            x_normal = -x_normal
        if flip_y:
            y_normal = -y_normal
        if flip_z:
            z_normal = -z_normal
        flipped_normals.append([x_normal, y_normal, z_normal])
    
    return flipped_normals

def flip_face_winding(faces):
    """
    Flip the winding order of faces (reverses normal direction)
    
    Parameters:
    faces -- list of faces to flip
    
    Returns:
    Faces with reversed winding order
    """
    flipped_faces = []
    
    for face in faces:
        # Reverse the order of vertices in the face
        # This effectively reverses the face normal direction
        flipped_face = face.copy()
        flipped_face.reverse()
        flipped_faces.append(flipped_face)
    
    return flipped_faces


def mirror_mesh(vertices, normals, faces, axis='x'):
    """
    Mirror the entire mesh along the specified axis
    
    Parameters:
    vertices -- list of vertices to mirror
    normals -- list of normals to mirror
    faces -- list of faces to mirror
    axis -- axis to mirror along ('x', 'y', or 'z')
    
    Returns:
    Mirrored vertices, normals, and faces
    """
    # Make deep copies to avoid modifying originals
    mirrored_vertices = []
    mirrored_normals = []
    
    # Determine which axis to mirror along
    axis_index = {'x': 0, 'y': 1, 'z': 2}.get(axis.lower(), 0)
    
    # Mirror vertices
    for vertex in vertices:
        new_vertex = vertex.copy()
        if len(new_vertex) > axis_index:
            new_vertex[axis_index] = -new_vertex[axis_index]
        mirrored_vertices.append(new_vertex)
    
    # Mirror normals
    for normal in normals:
        new_normal = normal.copy()
        new_normal[axis_index] = -new_normal[axis_index]
        mirrored_normals.append(new_normal)
    
    # Reverse face winding to maintain correct orientation after mirroring
    mirrored_faces = flip_face_winding(faces)
    
    # Update the X normal component in vertices
    for i in range(len(mirrored_vertices)):
        if i < len(mirrored_normals) and len(mirrored_vertices[i]) >= 4:
            mirrored_vertices[i][3] = mirrored_normals[i][0]
    
    print(f"Mirrored mesh along {axis.upper()} axis")
    return mirrored_vertices, mirrored_normals, mirrored_faces

def swap16(xswap):
  if xswap < 0:
    xswap = (1 << 16) + xswap
  return int.from_bytes(xswap.to_bytes(2, byteorder='little'), byteorder='big')



def parse_obj(obj_path):
    vertices = []
    faces = []
    normals = []
    uvs = []
    
    # For mapping face vertex to UV and normal
    vertex_uv_map = {}  # Maps (face_idx, vertex_idx) to uv_idx
    vertex_normal_map = {}  # Maps (face_idx, vertex_idx) to normal_idx
    
    # Track objects and groups
    objects = {}  # {object_name: list of face indices}
    groups = {}   # {group_name: list of face indices}
    current_object = "default"  # Default object name
    current_group = "default"   # Default group name
    objects[current_object] = []
    groups[current_group] = []
    
    face_count = 0  # To track face indices
    
    print("Loading OBJ file:", obj_path)
    
    with open(obj_path, 'r') as f:
        for line in f:
            tokens = line.split()
            if not tokens:
                continue
                
            if tokens[0] == 'o':  # Object definition
                current_object = ' '.join(tokens[1:]) if len(tokens) > 1 else f"unnamed_object_{len(objects)}"
                print(f"Found object: {current_object}")
                if current_object not in objects:
                    objects[current_object] = []
                    
            elif tokens[0] == 'g':  # Group definition
                current_group = ' '.join(tokens[1:]) if len(tokens) > 1 else f"unnamed_group_{len(groups)}"
                print(f"Found group: {current_group}")
                if current_group not in groups:
                    groups[current_group] = []
                    
            elif tokens[0] == 'v':
                try:
                    # Parse only XYZ and don't add the 4th component yet
                    vertex = [float(x) for x in tokens[1:]]
                    vertices.append(vertex)  # Store just the XYZ
                except ValueError:
                    print(f"Warning: Invalid vertex data on line: {line.strip()}")
                    
            elif tokens[0] == 'vn':
                try:
                    # Read all three components of the normal vector
                    x_normal = float(tokens[1])
                    y_normal = float(tokens[2])
                    z_normal = float(tokens[3])
                    normals.append([x_normal, y_normal, z_normal]) 
                except ValueError:
                    print(f"Warning: Invalid normal data on line: {line.strip()}")        
                    
            elif tokens[0] == 'vt':
                try:
                    u = float(tokens[1])
                    v = 1.0 - float(tokens[2])  # Flip V coordinate
                    uvs.append((u, v)) 
                except ValueError:
                    print(f"Warning: Invalid UV data on line: {line.strip()}")
                    
            elif tokens[0] == 'f':
                try:
                    # Parse vertex/texture/normal indices
                    face_vertices = []
                    
                    for i, part in enumerate(tokens[1:]):
                        indices = part.split('/')
                        v_idx = int(indices[0])
                        face_vertices.append(v_idx)
                        
                        # Store UV mapping if present (indices[1])
                        if len(indices) > 1 and indices[1]:
                            uv_idx = int(indices[1])
                            # Store mapping of this vertex in this face to this UV
                            vertex_uv_map[(face_count, v_idx)] = uv_idx - 1  # OBJ indices are 1-based
                        
                        # If normal index is provided, map it to vertex within this face
                        if len(indices) >= 3 and indices[2]:
                            n_idx = int(indices[2])
                            # Store normal for this vertex in this specific face
                            vertex_normal_map[(face_count, v_idx)] = n_idx - 1  # OBJ indices are 1-based
                    
                    faces.append(face_vertices)
                    
                    # Track face in current object and group
                    objects[current_object].append(face_count)
                    groups[current_group].append(face_count)
                    face_count += 1
                    
                except ValueError:
                    print(f"Warning: Invalid face data on line: {line.strip()}")

    # Print statistics about objects and groups
    print("\nOBJ File Structure Summary:")
    print(f"Total vertices: {len(vertices)}")
    print(f"Total faces: {len(faces)}")
    print(f"Total normals: {len(normals)}")
    print(f"Total UV coordinates: {len(uvs)}")
    
    # Print objects and their face counts
    print("\nObjects:")
    for obj_name, obj_faces in objects.items():
        print(f"  {obj_name}: {len(obj_faces)} faces")
        
    # Print groups and their face counts
    print("\nGroups:")
    for group_name, group_faces in groups.items():
        print(f"  {group_name}: {len(group_faces)} faces")

    # Create vertices with associated data (including X component of normal)
    vertices_with_normal_x = []
    for i, vertex in enumerate(vertices):
        vertex_idx = i + 1  # OBJ indices are 1-based
        
        full_vertex = vertex.copy()
        
        # Add the X normal component - we'll use 0.0 by default
        # Later in save_obj_to_binary we'll match it with the correct normal
        full_vertex.append(0.0)
        vertices_with_normal_x.append(full_vertex)

    # Store the object/group structure and vertex mapping for later use
    global obj_structure
    obj_structure = {
        'objects': {obj_name: len(obj_faces) for obj_name, obj_faces in objects.items()},
        'groups': {group_name: len(group_faces) for group_name, group_faces in groups.items()},
        'object_faces': objects,
        'group_faces': groups,
        'vertex_uv_map': vertex_uv_map,  # Store the vertex to UV mapping
        'vertex_normal_map': vertex_normal_map  # Store the vertex to normal mapping
    }

    return vertices_with_normal_x, faces, normals, uvs, vertex_uv_map, vertex_normal_map



def center_model(vertices):
    """
    Centers the model by calculating the center point and subtracting it from all vertices
    
    Parameters:
    vertices -- list of vertices to center
    
    Returns:
    Centered vertices and the calculated center point
    """
    if not vertices:
        return vertices, (0, 0, 0)
    
    # Calculate min and max for XYZ coordinates only (not normal components)
    min_x = min(vertex[0] for vertex in vertices)
    max_x = max(vertex[0] for vertex in vertices)
    min_y = min(vertex[1] for vertex in vertices if len(vertex) > 1)
    max_y = max(vertex[1] for vertex in vertices if len(vertex) > 1)
    min_z = min(vertex[2] for vertex in vertices if len(vertex) > 2)
    max_z = max(vertex[2] for vertex in vertices if len(vertex) > 2)
    
    # Calculate center point
    center_x = (min_x + max_x) / 2
    center_y = (min_y + max_y) / 2
    center_z = (min_z + max_z) / 2
    
    print(f"Model center point: ({center_x:.4f}, {center_y:.4f}, {center_z:.4f})")
    print(f"Model dimensions: X={max_x-min_x:.4f}, Y={max_y-min_y:.4f}, Z={max_z-min_z:.4f}")
    
    # Subtract center from each vertex to center the model
    centered_vertices = []
    for vertex in vertices:
        new_vertex = vertex.copy()
        # Only adjust the first 3 components (XYZ), leave any normal components unchanged
        new_vertex[0] = vertex[0] - center_x
        if len(vertex) > 1:
            new_vertex[1] = vertex[1] - center_y
        if len(vertex) > 2:
            new_vertex[2] = vertex[2] - center_z
        centered_vertices.append(new_vertex)
    
    return centered_vertices, (center_x, center_y, center_z)


def save_vertices(vertices, obj_path, model_bounds=None):
    with open(obj_path, 'ab') as f:
        global overall_model_bounds
        if overall_model_bounds['initialized']:
            # Calculate the maximum range across all axes for uniform scaling
            x_range = overall_model_bounds['max_x'] - overall_model_bounds['min_x']
            y_range = overall_model_bounds['max_y'] - overall_model_bounds['min_y']
            z_range = overall_model_bounds['max_z'] - overall_model_bounds['min_z']
            max_range = max(x_range, y_range, z_range)
            
            # Apply expansion factor
            expansion_factor = 0.1  # 10% expansion
            expanded_range = max_range * (1 + expansion_factor)
            
            # Use half-range as bounds for consistent scaling
            min_coord = -expanded_range/2
            max_coord = expanded_range/2
            
            print(f"Using uniform scaling bounds: {min_coord} to {max_coord}")
        elif model_bounds and 'min_coord' in model_bounds and 'max_coord' in model_bounds:
            min_coord = model_bounds['min_coord']
            max_coord = model_bounds['max_coord']
            print(f"Using provided model bounds: {min_coord} to {max_coord}")
        else:
            # Calculate from current vertices as a last resort
            if vertices:
                coords = [coord for vertex in vertices for coord in vertex[:3]]
                min_coord = min(coords)
                max_coord = max(coords)
                print(f"Using calculated bounds: {min_coord} to {max_coord}")
            else:
                min_coord = -1.0
                max_coord = 1.0
                print("No vertices provided. Using default bounds.")
        
        
        # Calculate the range and scaling factor
        bounds_range = max_coord - min_coord
        
        data_start = f.tell()
        # Write placeholder header - we'll update it later
        header_placeholder = bytes.fromhex("00 00 00 00 00 00 00 00 08 00 01 00 0D 00 00 00")
        f.write(header_placeholder)
        
        # Write vertex data using consistent scaling
        for vertex in vertices:
            for coord in vertex:
                # This is the key change: We scale the values using the overall bounds,
                # but we're writing vertices that have already been centered
                if bounds_range > 0:
                    scaled_coord = 0.5 + (coord / bounds_range)  # Center around 0.5 in normalized space
                else:
                    scaled_coord = 0.5  # Default to center if no range
                
                # Clamp to ensure we stay in valid range
                scaled_coord = max(0.0, min(1.0, scaled_coord))
                
                # Convert to 16-bit value for writing
                relative_coord = swap16(round((scaled_coord * 65535) - 32768))
                f.write(bytes.fromhex(format(relative_coord, '04X')))
        
        data_end = f.tell()
        data_size = data_end - data_start - 16
        data_size2 = data_end - data_start
        
    # Open file again to update header and offsets
    with open(obj_path, 'r+b') as f:
        # Update header with count and size
        f.seek(data_start)
        f.write(struct.pack('<I', int(data_size / 8)))  # First 4 bytes: count
        f.write(struct.pack('<I', data_size))          # Second 4 bytes: size
        
        # Update table entry
        f.seek(0x140)
        f.write(struct.pack('<I', data_start))        # Offset
        f.write(struct.pack('<I', data_size2))        # Size1
        f.write(struct.pack('<I', data_size2))        # Size2



face_data_size = 0
face_data_start = 0

def save_faces(faces, obj_path):
    global face_data_start
    with open(obj_path, 'ab') as f: 
        face_data_start = f.tell()
        data_start = f.tell()
        header = bytes.fromhex("FF FF FF FF FF FF FF FF 04 00 01 00 2A 00 00 00")
        f.write(header)
        for face in faces:
            for vertex_index in face:
                f.write(struct.pack('<i', vertex_index - 1))
        data_end = f.tell()
        face_data_size = data_end - data_start - 16
        data_size2 = data_end - data_start
        with open(obj_path, 'r+b') as f:
            f.seek(data_start)
            f.write(struct.pack('<I', int(face_data_size / 4)))
            f.write(struct.pack('<I', face_data_size))
            f.seek(0xF8)
            f.write(struct.pack('<I', data_start))
            f.write(struct.pack('<I', data_size2))
            f.write(struct.pack('<I', data_size2))

def calculate_faces(faces, obj_path):
    global face_data_size
    face_data_buffer = io.BytesIO()
    with open(obj_path, 'ab') as f: 
        data_start = face_data_buffer.tell()
        for face in faces:
            for vertex_index in face:
                face_data_buffer.write(struct.pack('<i', vertex_index - 1))
        data_end = face_data_buffer.tell()
        face_data_size = data_end - data_start
        print(face_data_size)

# Add this normalize_vector function at the top level (outside any other functions)
def normalize_vector(v):
    """Normalize a vector to unit length"""
    length = (v[0]**2 + v[1]**2 + v[2]**2)**0.5
    if length > 0:
        return [v[0]/length, v[1]/length, v[2]/length]
    return [0.0, 1.0, 0.0]  # Default up vector if length is zero

def save_normals_uvs(normals, uvs, obj_path):
    with open(obj_path, 'ab') as f:
        # Normalize all normal vectors before processing
        normalized_normals = [normalize_vector(normal) for normal in normals]
        
        # Don't need min_x_normal anymore since X is stored with vertex data
        if normalized_normals:
            min_y_normal = min(normal[1] for normal in normalized_normals)
            min_z_normal = min(normal[2] for normal in normalized_normals)
            # Normals should be in the range [-1, 1] already
        else:
            min_y_normal = 0
            min_z_normal = 0
            
        if uvs:   
           min_u_uv = min(uv[0] for uv in uvs)
           min_v_uv = min(uv[1] for uv in uvs)
           max_u_uv = max(uv[0] for uv in uvs)
           max_v_uv = max(uv[1] for uv in uvs)
        else:
            min_u_uv = 0
            min_v_uv = 0
            max_u_uv = 1
            max_v_uv = 1
    
        data_start = f.tell()
        header = bytes.fromhex("FF FF FF FF FF FF FF 00 28 00 0A 00 25 00 00 00")
        f.write(header)
        
        # Ensure we have matching counts of normals and UVs
        count = min(len(normalized_normals), len(uvs))
        
        for i in range(count):
            normal = normalized_normals[i]
            uv = uvs[i]
            
            _, y_normal, z_normal = normal
            u_uv, v_uv = uv

            # For SNORM format, scale directly to -32767 to 32767 range
            # No clamping - we assume normals are unit vectors in the range [-1, 1]
            relative_y_normal = swap16(round(y_normal * 32767))
            relative_z_normal = swap16(round(z_normal * 32767))
            
            # Write the normal Y and Z components
            f.write(bytes.fromhex(format(relative_y_normal, '04X')))
            f.write(bytes.fromhex(format(relative_z_normal, '04X')))

            # For UVs, do proper normalization
            u_range = max_u_uv - min_u_uv
            v_range = max_v_uv - min_v_uv
            
            # Calculate and write u_uv
            scaled_u_uv = (u_uv - min_u_uv) / u_range if u_range > 0 else 0.5
            relative_u_uv = swap16(round(scaled_u_uv * 65535))
            f.write(bytes.fromhex(format(relative_u_uv, '04X')))

            # Calculate and write v_uv
            scaled_v_uv = (v_uv - min_v_uv) / v_range if v_range > 0 else 0.5
            relative_v_uv = swap16(round(scaled_v_uv * 65535))
            f.write(bytes.fromhex(format(relative_v_uv, '04X')))
            
            # Write additional UV data (texture coordinates for multiple channels)
            for _ in range(8):
                f.write(bytes.fromhex(format(relative_u_uv, '04X')) + bytes.fromhex(format(relative_v_uv, '04X')))

        data_end = f.tell()
        data_size = data_end - data_start - 16
        data_size2 = data_end - data_start
        with open(obj_path, 'r+b') as f:
            f.seek(data_start)
            f.write(struct.pack('<I', int(data_size / 40)))
            f.write(struct.pack('<I', data_size))
            f.seek(0x158)
            f.write(struct.pack('<I', data_start))
            f.write(struct.pack('<I', data_size2))
            f.write(struct.pack('<I', data_size2))






def save_material_metadata(selected_material_key, output_file):
    material_metadata = materials.materials.get(selected_material_key)
    if material_metadata:
        with open(output_file, "ab") as f:  
            data_start = f.tell()
            f.write(bytes.fromhex(format(material_metadata["metadata"])))
            data_end = f.tell()
            with open(output_file, 'r+b') as f:
                f.seek(0xDC)
                f.write(struct.pack('<I', data_start))


def save_material(selected_material_key, output_file):
    material_data = materials.materials.get(selected_material_key)
    if material_data:
        with open(output_file, "ab") as f:  
            data_start = f.tell()
            f.write(bytes.fromhex(format(material_data["data"])))
            data_end = f.tell()
            data_size = data_end - data_start
            with open(output_file, 'r+b') as f:
                f.seek(0xE0)
                f.write(struct.pack('<I', data_start))
                f.write(struct.pack('<I', data_size))
                f.write(struct.pack('<I', data_size))


    else:
        print(f"Material not found: {selected_material_key}")

def save_header(output_file):
    header_data = bytes.fromhex("62 75 72 47 01 01 00 00 39 03 00 00 FF FF FF FF 0F 00 00 00")
    with open(output_file, 'ab') as f:  # 'wb' for new file 
        f.write(header_data) 

mesh_data_start = 0        
def save_mesh_data(obj_path, tag_dictionary):  
    global face_data_size
    global mesh_data_start
    tag_data_hex = ''.join(tag_dictionary.values()).replace(' ', '')
    tag_data = bytes.fromhex(tag_data_hex)
    with open(obj_path, 'ab') as f: 
        data_start = f.tell()
        mesh_data_start = f.tell()
        f.write(tag_data)
        data_end = f.tell()
        data_size = data_end - data_start
        with open(obj_path, 'r+b') as f:
            if tag_dictionary == mesh_data0:
                f.seek(0x50)
                f.write(struct.pack('<I', data_start))
                f.write(struct.pack('<I', data_size))
                f.write(struct.pack('<I', data_size))
            elif tag_dictionary == mesh_data1:
                    f.seek(0x68)
                    f.write(struct.pack('<I', data_start))
                    f.write(struct.pack('<I', data_size))
                    f.write(struct.pack('<I', data_size))
            elif tag_dictionary == mesh_data2:
                    f.seek(0x80)
                    f.write(struct.pack('<I', data_start))
                    f.write(struct.pack('<I', data_size))
                    f.write(struct.pack('<I', data_size))
            elif tag_dictionary == mesh_data3:
                    f.seek(0x98)
                    f.write(struct.pack('<I', data_start))
                    f.write(struct.pack('<I', data_size))
                    f.write(struct.pack('<I', data_size))
            elif tag_dictionary == mesh_data4:
                    f.seek(0xB0)
                    f.write(struct.pack('<I', data_start))
                    f.write(struct.pack('<I', data_size))
                    f.write(struct.pack('<I', data_size))
            elif tag_dictionary == mesh_data5:
                    f.seek(0xC8)
                    f.write(struct.pack('<I', data_start))
                    f.write(struct.pack('<I', data_size))
                    f.write(struct.pack('<I', data_size))
        return data_start


def save_tag_data(obj_path, tag_dictionary):
    tag_data = bytes.fromhex(tag_dictionary["data"])
    with open(obj_path, 'ab') as f:  # 'ab' to append
        data_start = f.tell()
        f.write(tag_data)
        data_end = f.tell()
        data_size = data_end - data_start
        with open(obj_path, 'r+b') as f:
            if tag_dictionary == skeleton_data:
                   f.seek(0x20)
                   f.write(struct.pack('<I', data_start))
                   f.write(struct.pack('<I', data_size))
                   f.write(struct.pack('<I', data_size))
            elif tag_dictionary == morph_data:
                   f.seek(0x38)
                   f.write(struct.pack('<I', data_start))
                   f.write(struct.pack('<I', data_size))
                   f.write(struct.pack('<I', data_size))
            elif tag_dictionary == vlay_data:
                   f.seek(0x110)
                   f.write(struct.pack('<I', data_start))
                   f.write(struct.pack('<I', data_size))
                   f.write(struct.pack('<I', data_size))
            elif tag_dictionary == vlay_data2:
                   f.seek(0x128)
                   f.write(struct.pack('<I', data_start))
                   f.write(struct.pack('<I', data_size))
                   f.write(struct.pack('<I', data_size))
            elif tag_dictionary == model_data:
                   f.seek(0x170)
                   f.write(struct.pack('<I', data_start))
                   f.write(struct.pack('<I', data_size))
                   f.write(struct.pack('<I', data_size))
            



def save_tag_metadata(output_file, tag_dictionary):
    tag_metadata = bytes.fromhex(tag_dictionary["metadata"])
    with open(output_file, 'ab') as f:  # 'ab' to append
        data_start = f.tell()
        f.write(tag_metadata)
        data_end = f.tell()
        data_size = data_end - data_start
        with open(output_file, 'r+b') as f:
            if tag_dictionary == vlay_tag:
                   f.seek(0x10C)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == vlay_tag2:
                   f.seek(0x124)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == material_tag:
                   f.seek(0xDC)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == model_tag:
                   f.seek(0x16C)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == mesh_tag0:
                   f.seek(0x4C)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == mesh_tag1:
                   f.seek(0x64)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == mesh_tag2:
                   f.seek(0x7C)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == mesh_tag3:
                   f.seek(0x94)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == mesh_tag4:
                   f.seek(0xAC)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == mesh_tag5:
                   f.seek(0xC4)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == indexbuffer_tag:
                   f.seek(0xF4)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == vertexbuffer_tag:
                   f.seek(0x13C)
                   f.write(struct.pack('<I', data_start))
            elif tag_dictionary == vertexbuffer1_tag:
                   f.seek(0x154)
                   f.write(struct.pack('<I', data_start))



def save_mesh_metadata(output_file, tag_dictionary):
    tag_metadata = bytes.fromhex(tag_dictionary["metadata_lods0"]) + \
                   bytes.fromhex(tag_dictionary["metadata_lod1"]) + \
                   bytes.fromhex(tag_dictionary["metadata_lod2"]) + \
                   bytes.fromhex(tag_dictionary["metadata_lod3"]) + \
                   bytes.fromhex(tag_dictionary["metadata_lod4"]) + \
                   bytes.fromhex(tag_dictionary["metadata_lod5"]) 
    with open(output_file, 'ab') as f:  
        f.write(tag_metadata)




def get_file_path():
    root = tk.Tk()
    root.withdraw()
    global input_file
    input_file = filedialog.askopenfilename(title="Select OBJ or modelbin file",filetypes=[("OBJ files", "*.obj"),("modelbin files", "*.modelbin")])

    if not input_file:
        print("No file selected.")
        return None  # Return none on cancellation

    if input_file.endswith('.obj'):
        vertices, faces, normals, uvs, vertex_uv_map, vertex_normal_map = parse_obj(input_file)
        return vertices, faces, normals, uvs
    elif input_file.endswith('.modelbin'):
        mesh_data_list, parser = load_modelbin(input_file)
    else:
        print("Unsupported file type.")

    return input_file









def save_obj_to_binary(self, selected_material):
    global mesh_data_start
    global face_data_size
    root = tk.Tk()
    root.withdraw() # Hide the main window 
    output_file = filedialog.asksaveasfilename(title="Select output", defaultextension=".modelbin", filetypes=[("modelbin file", "*.modelbin")])   
    if not output_file:
        return  # User canceled

    # Get the selected faces from the GUI's tree view
    selected_face_indices = self.get_selected_faces()
    
    with open(output_file, 'wb') as f:
        try:
            # Write header
            save_header(output_file) 
            
            # Load OBJ data if not already loaded
            if not self.loaded_data:
                vertices, faces, normals, uvs, vertex_uv_map, vertex_normal_map = parse_obj(input_file)
            else:
                vertices, faces, normals, uvs = self.loaded_data
                # We need to get the mappings from parse_obj
                _, _, _, _, vertex_uv_map, vertex_normal_map = parse_obj(input_file)
            
            # Filter faces if selection is active
            if selected_face_indices is not None and len(selected_face_indices) > 0:
                # Only keep the faces that are in the selected indices
                filtered_faces = [faces[i] for i in selected_face_indices if i < len(faces)]
                
                # Find which vertices are actually used by these faces
                used_vertex_indices = set()
                for face in filtered_faces:
                    for vertex_idx in face:
                        # OBJ indices are 1-based, but our arrays are 0-based
                        used_vertex_indices.add(vertex_idx - 1)
                
                # Create a mapping from old vertex indices to new ones
                old_to_new_index = {}
                new_vertices = []
                new_normals = []
                new_uvs = []
                
                # First pass: collect all vertex-UV and vertex-normal mappings for selected faces
                vertex_to_uv = {}
                vertex_to_normal = {}
                direct_vertex_normal = {}
                
                # Build this mapping from all faces, ensuring consistent normals
                vertex_normal_consistency = {}
                
                # First pass: gather all normal candidates for each vertex
                for orig_face_idx in selected_face_indices:
                    if orig_face_idx < len(faces):
                        face = faces[orig_face_idx]
                        for vertex_idx in face:
                            old_zero_based = vertex_idx - 1
                            normal_key = (orig_face_idx, vertex_idx)
                            
                            if normal_key in vertex_normal_map and vertex_normal_map[normal_key] < len(normals):
                                normal_idx = vertex_normal_map[normal_key]
                                
                                # Store this normal as a candidate for this vertex
                                if old_zero_based not in vertex_normal_consistency:
                                    vertex_normal_consistency[old_zero_based] = []
                                vertex_normal_consistency[old_zero_based].append(normal_idx)

                # Second pass: choose the most frequent normal for each vertex
                for vertex_idx, normal_indices in vertex_normal_consistency.items():
                    if normal_indices:
                        # Count occurrences of each normal
                        normal_counts = {}
                        for n_idx in normal_indices:
                            normal_counts[n_idx] = normal_counts.get(n_idx, 0) + 1
                            
                        # Find the most common normal for this vertex
                        most_common_normal = max(normal_counts.items(), key=lambda x: x[1])[0]
                        direct_vertex_normal[vertex_idx] = most_common_normal

                # Then the regular face processing
                for new_face_idx, face in enumerate(filtered_faces):
                    orig_face_idx = selected_face_indices[new_face_idx]
                    for vertex_idx in face:
                        old_zero_based = vertex_idx - 1
                        
                        # Check if this vertex-face pair has a UV mapping
                        uv_key = (orig_face_idx, vertex_idx)
                        if uv_key in vertex_uv_map and vertex_uv_map[uv_key] < len(uvs):
                            vertex_to_uv[old_zero_based] = vertex_uv_map[uv_key]
                        
                        # Check if this vertex-face pair has a normal mapping
                        normal_key = (orig_face_idx, vertex_idx)
                        if normal_key in vertex_normal_map and vertex_normal_map[normal_key] < len(normals):
                            vertex_to_normal[old_zero_based] = vertex_normal_map[normal_key]
                
                # Build new vertex arrays with only used vertices
                for old_idx in sorted(used_vertex_indices):
                    # Map the old index to the new position
                    old_to_new_index[old_idx] = len(new_vertices)
                    
                    # Add this vertex to our new arrays
                    if old_idx < len(vertices):
                        new_vertex = vertices[old_idx].copy()
                        
                        # Prioritize the direct vertex-normal mapping for multi-object cases
                        normal_idx = None
                        
                        # Try to get normal from direct mapping first (most accurate for multi-object)
                        if old_idx in direct_vertex_normal:
                            normal_idx = direct_vertex_normal[old_idx]
                            if normal_idx < len(normals):
                                # Ensure vertex has room for the normal X component
                                if len(new_vertex) < 4:
                                    new_vertex.append(0.0)  # Add space for X normal if not present
                                # Set the X component of the normal
                                new_vertex[3] = normals[normal_idx][0]
                        # Fall back to face-specific mapping
                        elif old_idx in vertex_to_normal:
                            normal_idx = vertex_to_normal[old_idx]
                        
                        # Update the X component of normal in the vertex
                        if normal_idx is not None and normal_idx < len(normals):
                            if len(new_vertex) >= 4:
                                new_vertex[3] = normals[normal_idx][0]
                        
                        new_vertices.append(new_vertex)
                    
                    # Add matching normal - prioritize direct mapping for better consistency
                    if old_idx in direct_vertex_normal:
                        normal_idx = direct_vertex_normal[old_idx]
                        if normal_idx < len(normals):
                            new_normals.append(normals[normal_idx])
                        else:
                            new_normals.append([0.0, 1.0, 0.0])  # Default up normal
                    elif old_idx in vertex_to_normal:
                        normal_idx = vertex_to_normal[old_idx]
                        new_normals.append(normals[normal_idx])
                    elif old_idx < len(normals):
                        new_normals.append(normals[old_idx])
                    else:
                        # Add default normal if needed
                        new_normals.append([0.0, 1.0, 0.0])
                    
                    # Add matching UV - with improved fallback handling
                    if old_idx in vertex_to_uv:
                        uv_idx = vertex_to_uv[old_idx]
                        new_uvs.append(uvs[uv_idx])
                    elif old_idx < len(uvs):
                        new_uvs.append(uvs[old_idx])
                    else:
                        # Add default UV if needed
                        new_uvs.append((0.5, 0.5))  # Center of texture
                
                # Normalize all normal vectors to ensure consistency
                for i in range(len(new_normals)):
                    new_normals[i] = normalize_vector(new_normals[i])
                    
                    # Make sure the X component in vertices matches the normalized normal
                    vertex_idx = i 
                    if vertex_idx < len(new_vertices) and len(new_vertices[vertex_idx]) >= 4:
                        new_vertices[vertex_idx][3] = new_normals[i][0]
                
                # Update face indices to point to our new vertex array
                new_faces = []
                for face in filtered_faces:
                    new_face = []
                    for vertex_idx in face:
                        # Convert from 1-based to 0-based, map, then convert back to 1-based
                        old_zero_based = vertex_idx - 1
                        new_zero_based = old_to_new_index[old_zero_based]
                        new_one_based = new_zero_based + 1
                        new_face.append(new_one_based)
                    new_faces.append(new_face)
                
                # Verify normals and vertices match in count
                if len(new_normals) != len(new_vertices):
                    print(f"Warning: Normal count ({len(new_normals)}) doesn't match vertex count ({len(new_vertices)})")
                    
                    # Make sure we have enough normals for all vertices
                    if len(new_normals) < len(new_vertices):
                        print("Adding missing normals")
                        new_normals.extend([normalize_vector([0.0, 1.0, 0.0])] * (len(new_vertices) - len(new_normals)))
                    else:
                        # Truncate excess normals
                        print("Truncating excess normals")
                        new_normals = new_normals[:len(new_vertices)]
                
                # Replace with our filtered data
                vertices = new_vertices
                normals = new_normals
                uvs = new_uvs
                faces = new_faces
                
                print(f"Combined mesh contains {len(vertices)} vertices, {len(faces)} faces, {len(normals)} normals, {len(uvs)} UVs")
           

            global overall_model_bounds

            if overall_model_bounds['initialized']:
                # Calculate the center of the entire model based on per-axis bounds
                center_x = (overall_model_bounds['min_x'] + overall_model_bounds['max_x']) / 2
                center_y = (overall_model_bounds['min_y'] + overall_model_bounds['max_y']) / 2
                center_z = (overall_model_bounds['min_z'] + overall_model_bounds['max_z']) / 2
    
                print(f"Using overall model center: ({center_x:.4f}, {center_y:.4f}, {center_z:.4f})")
                
                # Subtract this center from all vertices to center the entire model while maintaining relative positions
                centered_vertices = []
                for vertex in vertices:
                    new_vertex = vertex.copy()
                    new_vertex[0] = vertex[0] - center_x
                    if len(vertex) > 1:
                        new_vertex[1] = vertex[1] - center_y
                    if len(vertex) > 2:
                        new_vertex[2] = vertex[2] - center_z
                    centered_vertices.append(new_vertex)
                
                vertices = centered_vertices
                print("Applied consistent centering to maintain relative object positions.")
            

           
            # Apply vertex flipping if selected
            if self.flip_vertex_x.get() or self.flip_vertex_y.get() or self.flip_vertex_z.get():
                print(f"Flipping vertices: X={self.flip_vertex_x.get()}, Y={self.flip_vertex_y.get()}, Z={self.flip_vertex_z.get()}")
                vertices = flip_vertices(vertices, 
                                      self.flip_vertex_x.get(), 
                                      self.flip_vertex_y.get(), 
                                      self.flip_vertex_z.get())

            # Apply normal flipping if selected
            if self.flip_normal_x.get() or self.flip_normal_y.get() or self.flip_normal_z.get():
                print(f"Flipping normals: X={self.flip_normal_x.get()}, Y={self.flip_normal_y.get()}, Z={self.flip_normal_z.get()}")
                normals = flip_normals(normals, 
                                     self.flip_normal_x.get(), 
                                     self.flip_normal_y.get(), 
                                     self.flip_normal_z.get())

            # Update vertex normal X components after flipping normals
            for i in range(len(vertices)):
                if i < len(normals) and len(vertices[i]) >= 4:
                    vertices[i][3] = normals[i][0]

            # Apply face winding flip if selected
            if self.flip_faces.get():
                print("Flipping face winding order")
                faces = flip_face_winding(faces)
            
            # Rest of the function remains unchanged...
            calculate_faces(faces, output_file)  # Calculate face_data_size
            vertex_count = len(vertices)  # Get vertex count
            
            save_tag_data(output_file, skeleton_tag) 
            save_tag_data(output_file, morph_tag)
            save_tag_data(output_file, mesh_tag) #lods0
            save_tag_data(output_file, mesh_tag) #lod1
            save_tag_data(output_file, mesh_tag) #lod2
            save_tag_data(output_file, mesh_tag) #lod3
            save_tag_data(output_file, mesh_tag) #lod4
            save_tag_data(output_file, mesh_tag) #lod5
            save_tag_data(output_file, material_tag) 
            save_tag_data(output_file, indexbuffer_tag) 
            save_tag_data(output_file, vlay_tag)
            save_tag_data(output_file, vlay_tag2)
            save_tag_data(output_file, vertexbuffer_tag)
            save_tag_data(output_file, vertexbuffer_tag)
            save_tag_data(output_file, model_tag)
            save_material_metadata(selected_material.get(), output_file) 
            save_tag_metadata(output_file, mesh_tag0) 
            save_tag_metadata(output_file, mesh_tag1)
            save_tag_metadata(output_file, mesh_tag2)
            save_tag_metadata(output_file, mesh_tag3)
            save_tag_metadata(output_file, mesh_tag4)
            save_tag_metadata(output_file, mesh_tag5)
            save_tag_metadata(output_file, indexbuffer_tag) 
            save_tag_metadata(output_file, vlay_tag) 
            save_tag_metadata(output_file, vlay_tag2)
            save_tag_metadata(output_file, vertexbuffer_tag) 
            save_tag_metadata(output_file, vertexbuffer1_tag) 
            save_tag_metadata(output_file, model_tag)
            #write skeleton
            save_tag_data(output_file, skeleton_data)
            save_tag_data(output_file, morph_data)
            #write material
            save_material(selected_material.get(), output_file)
            
            #write mesh data

            # Add this right before the mesh_positions.append lines
            # Update mesh data with scale and position values from the UI
            for mesh_data in [mesh_data0, mesh_data1, mesh_data2, mesh_data3, mesh_data4, mesh_data5]:
                mesh_data["VertScaleX"] = float_to_hex(self.scale_x.get())
                mesh_data["VertScaleY"] = float_to_hex(self.scale_y.get())
                mesh_data["VertScaleZ"] = float_to_hex(self.scale_z.get())
                mesh_data["VertPositionX"] = float_to_hex(self.pos_x.get())
                mesh_data["VertPositionY"] = float_to_hex(self.pos_y.get())
                mesh_data["VertPositionZ"] = float_to_hex(self.pos_z.get())
            mesh_positions = []  # Store mesh data positions
            
            # First, write all mesh data structures
            mesh_positions.append(save_mesh_data(output_file, mesh_data0))
            mesh_positions.append(save_mesh_data(output_file, mesh_data1))
            mesh_positions.append(save_mesh_data(output_file, mesh_data2))
            mesh_positions.append(save_mesh_data(output_file, mesh_data3))
            mesh_positions.append(save_mesh_data(output_file, mesh_data4))
            mesh_positions.append(save_mesh_data(output_file, mesh_data5))
            
            # Now patch each mesh data with vertex count and face index count
            with open(output_file, 'r+b') as patch_file:
                for pos in mesh_positions:
                    patch_file.seek(pos + 0x27)
                    face_index_count = int(face_data_size / 4)
                    face_count = int(face_index_count / 3)
                    patch_file.write(struct.pack('<I', face_index_count))  # face index count
                    patch_file.write(struct.pack('<I', face_count))  # face count
                    ratio = vertex_count / face_count if face_count > 0 else 1
                    patch_file.write(struct.pack('<f', ratio))  # face index ratio
                    patch_file.write(struct.pack('<I', vertex_count))  # vertex count

            save_faces(faces, output_file)
            #write model
            save_tag_data(output_file, vlay_data)
            save_tag_data(output_file, vlay_data2)


            if overall_model_bounds['initialized']:
                # Create a compatible bounds object from our per-axis bounds
                x_range = overall_model_bounds['max_x'] - overall_model_bounds['min_x']
                y_range = overall_model_bounds['max_y'] - overall_model_bounds['min_y']
                z_range = overall_model_bounds['max_z'] - overall_model_bounds['min_z']
                max_range = max(x_range, y_range, z_range)
                
                # Apply expansion factor
                expansion_factor = 0.1  # 10% expansion
                expanded_range = max_range * (1 + expansion_factor)
                
                unified_bounds = {
                    'min_coord': -expanded_range/2,
                    'max_coord': expanded_range/2
                }
                print(f"Using uniform scaling bounds: {unified_bounds['min_coord']} to {unified_bounds['max_coord']}")
                save_vertices(vertices, output_file, unified_bounds)
            else:
                # Fall back to object-specific bounds if no overall bounds available
                save_vertices(vertices, output_file, self.model_bounds)

            save_normals_uvs(normals, uvs, output_file)
            save_tag_data(output_file, model_data)
            file_size = os.path.getsize(output_file) #calculate the overall file size to be added back into the header
            print("File Size is :", file_size)
            f.seek(0x0C)
            f.write(struct.pack('<I', file_size))

        except FileNotFoundError:
            print("Error: Selected file not found.")
        except Exception as e:
            print(f"Error during parsing: {e}")

        return






if __name__ == "__main__":
  root = tk.Tk()
  app = App(root)
  root.mainloop()