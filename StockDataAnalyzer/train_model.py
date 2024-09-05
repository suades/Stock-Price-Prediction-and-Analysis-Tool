import pandas as pd
from sklearn.linear_model import Ridge
from sklearn.metrics import mean_absolute_error, mean_squared_error
from sklearn.model_selection import KFold, cross_val_score
import numpy as np

# Load the data
data = pd.read_csv('../individual_stocks_5yr/individual_stocks_5yr/AAPL_data.csv')

# Preprocess data
data['Date'] = pd.to_datetime(data['Date'])
data = data.sort_values('Date')
X = np.arange(len(data)).reshape(-1, 1)  # Example: time series as a feature
y = data['Close'].values

# Define model
model = Ridge()

# Implement k-Fold Cross-Validation
kf = KFold(n_splits=5, shuffle=True, random_state=42)
mae_scores = cross_val_score(model, X, y, scoring='neg_mean_absolute_error', cv=kf)
rmse_scores = cross_val_score(model, X, y, scoring='neg_root_mean_squared_error', cv=kf)

# Fit the model on the entire dataset
model.fit(X, y)
predicted = model.predict(X)

# Calculate errors
mae = mean_absolute_error(y, predicted)
rmse = np.sqrt(mean_squared_error(y, predicted))

print(f'Cross-Validated MAE: {-np.mean(mae_scores):.4f}')
print(f'Cross-Validated RMSE: {-np.mean(rmse_scores):.4f}')
print(f'Training MAE: {mae:.4f}')
print(f'Training RMSE: {rmse:.4f}')

# Save the model
import joblib
joblib.dump(model, 'stock_price_predictor_ridge.pkl')
