path = '/Users/hart/Documents/GitHub/SyntheticCreditCard/SyntheticCreditCardData/yolo'

import os
import cv2
import numpy as np
import supervision as sv

import random
from pathlib import Path

# Define the folders for train and valid images
train_folder = Path(path) / 'train'


def display_random_image_with_bboxes():
    # Select a random image from either the train or valid folder
    image_folder = random.choice([train_folder / 'images'])#, valid_folder / 'images'])

    image_files = list(image_folder.glob('*.jpg'))  # Adjust file extension if needed
    selected_image_path = random.choice(image_files)

    # Read the selected image
    image = cv2.imread(str(selected_image_path))
    
    # Assuming the bounding boxes and labels are stored in a corresponding .txt file in the 'labels' subfolder
    label_path = (selected_image_path.parent.parent / 'labels' / (selected_image_path.stem + '.txt'))
    
    # Lists to store detection data
    xyxy_boxes = []
    confidences = []
    class_ids = []

    if label_path.exists():
        with open(label_path, 'r') as f:
            for line in f.readlines():
                print(line)
                data = line.strip().split()
                class_id = int(data[0])
                x_center, y_center, width, height = map(float, data[1:5])
                
                # Convert YOLO format to bounding box coordinates (xyxy format)
                x1 = (x_center - width / 2) * image.shape[1]
                y1 = (y_center - height / 2) * image.shape[0]
                x2 = (x_center + width / 2) * image.shape[1]
                y2 = (y_center + height / 2) * image.shape[0]
                
                xyxy_boxes.append([x1, y1, x2, y2])
                class_ids.append(class_id)
                confidences.append(1.0)  # Assuming 100% confidence for ground truth
    else:
        print(f"No label file found for image: {label_path}")
        return
    
    if not xyxy_boxes:
        print("No bounding boxes found")
        return
    
    # Create Detections object
    detections = sv.Detections(
        xyxy=np.array(xyxy_boxes),
        confidence=np.array(confidences),
        class_id=np.array(class_ids)
    )
    
    # Create label formatter
    label_format = lambda class_id, confidence: f"Ball {class_id}"
    
    # Create box annotator
    box_annotator = sv.BoxAnnotator(
        thickness=2
    )
    
    # Draw bounding boxes on the image
    annotated_image = box_annotator.annotate(
        scene=image.copy(), 
        detections=detections,
    )

    label_annotator = sv.LabelAnnotator(
        text_scale=0.5,  # Make labels smaller
        text_padding=1
    )
    annotated_image = label_annotator.annotate(scene=annotated_image, detections=detections)

    cv2.imshow("Image with YOLO BBoxes", annotated_image)

#Main loop to display images until a key is pressed
while True:
    display_random_image_with_bboxes()
    if cv2.waitKey(0) & 0xFF == 27:  # Press 'Esc' to exit
        break

cv2.destroyAllWindows()

